use std::collections::{HashSet, VecDeque};
use std::fs;
use std::path::{Path, PathBuf};
use std::time::{Instant, SystemTime, UNIX_EPOCH};

use rusqlite::{params, Connection, OptionalExtension, Transaction};
use walkdir::WalkDir;

use crate::assets::{
    build_item, infer_asset_type, is_image_file, is_media_file, should_scan_entry, AssetItem,
    AssetScanOptions,
};
use crate::config::{path_to_string, AppConfig};
use crate::thumbnails::ThumbnailCache;
use crate::unity_usage::UnityUsageIndex;

struct ExistingAsset {
    size_bytes: u64,
    modified_ms: u64,
    indexed_ms: u64,
    thumbnail_path: Option<String>,
}

pub struct AssetIndex {
    conn: Connection,
    cache_root: PathBuf,
}

impl AssetIndex {
    pub fn open(config: &AppConfig) -> Result<Self, String> {
        let cache_root = PathBuf::from(&config.cache_root);
        fs::create_dir_all(&cache_root).map_err(|err| {
            format!(
                "failed to create cache root {}: {err}",
                cache_root.display()
            )
        })?;
        let db_path = cache_root.join("asset-index.sqlite");
        let conn = Connection::open(&db_path)
            .map_err(|err| format!("failed to open asset index {}: {err}", db_path.display()))?;
        migrate(&conn)?;
        Ok(Self { conn, cache_root })
    }

    pub fn refresh(
        &mut self,
        config: &AppConfig,
        options: &AssetScanOptions,
    ) -> Result<(), String> {
        let started = Instant::now();
        let root_filter = (!options.root_ids.is_empty()).then(|| {
            options
                .root_ids
                .iter()
                .map(String::as_str)
                .collect::<HashSet<_>>()
        });
        let scan_roots: Vec<_> = config
            .scan_roots
            .iter()
            .filter(|root| {
                if !options.include_raw && !root.default_visible {
                    return false;
                }
                root_filter
                    .as_ref()
                    .map(|ids| ids.contains(root.id.as_str()))
                    .unwrap_or(true)
            })
            .collect();
        let thumbnail_cache = ThumbnailCache::new(&self.cache_root)?;
        let unity_usage = if scan_roots.iter().any(|root| root.source != "ai-infra") {
            UnityUsageIndex::build(config)
        } else {
            UnityUsageIndex::default()
        };
        let now = now_ms();
        let tx = self
            .conn
            .transaction()
            .map_err(|err| format!("failed to start asset index transaction: {err}"))?;
        let mut scanned_roots = 0usize;
        let mut scanned_files = 0usize;

        for root in scan_roots {
            let root_path = PathBuf::from(&root.path);
            if !root_path.exists() {
                continue;
            }
            scanned_roots += 1;

            let mut seen = HashSet::new();
            for entry in WalkDir::new(&root_path)
                .follow_links(false)
                .into_iter()
                .filter_entry(|entry| should_scan_entry(entry.path()))
            {
                let entry = match entry {
                    Ok(entry) => entry,
                    Err(_) => continue,
                };
                let path = entry.path();
                if !path.is_file() || !is_media_file(path) {
                    continue;
                }

                scanned_files += 1;
                let absolute_path = path_to_string(path);
                seen.insert(absolute_path.clone());
                let metadata = match path.metadata() {
                    Ok(metadata) => metadata,
                    Err(_) => continue,
                };
                let modified_ms = metadata
                    .modified()
                    .ok()
                    .and_then(|time| time.duration_since(UNIX_EPOCH).ok())
                    .map(|duration| duration.as_millis() as u64)
                    .unwrap_or(0);
                let size_bytes = metadata.len();
                let existing = find_existing(&tx, &absolute_path)?;
                let thumbnail_path = if let Some(existing) = existing.as_ref() {
                    if existing.size_bytes == size_bytes && existing.modified_ms == modified_ms {
                        existing.thumbnail_path.clone()
                    } else {
                        build_thumbnail(&thumbnail_cache, path, modified_ms, size_bytes)
                    }
                } else {
                    build_thumbnail(&thumbnail_cache, path, modified_ms, size_bytes)
                };
                let indexed_ms = existing
                    .as_ref()
                    .filter(|asset| {
                        asset.size_bytes == size_bytes && asset.modified_ms == modified_ms
                    })
                    .map(|asset| asset.indexed_ms)
                    .unwrap_or(now);
                let relative_path = path
                    .strip_prefix(&root_path)
                    .unwrap_or(path)
                    .to_string_lossy()
                    .replace('\\', "/");
                let asset_type = infer_asset_type(path, &root.id);
                let usage = unity_usage.usage_for(root, path, &relative_path, &asset_type);

                if let Some(item) =
                    build_item(root, &root_path, path, thumbnail_path, usage, indexed_ms)
                {
                    upsert(&tx, &item)?;
                }
            }

            delete_missing(&tx, &root.id, &seen)?;
        }

        tx.commit()
            .map_err(|err| format!("failed to commit asset index: {err}"))?;
        eprintln!(
            "[asset-studio] refresh roots={} files={} include_raw={} root_ids={} elapsed_ms={}",
            scanned_roots,
            scanned_files,
            options.include_raw,
            options.root_ids.join(","),
            started.elapsed().as_millis()
        );
        Ok(())
    }

    pub fn list(&self, include_raw: bool) -> Result<Vec<AssetItem>, String> {
        let query = if include_raw {
            LIST_ALL_QUERY
        } else {
            LIST_DEFAULT_QUERY
        };
        let mut stmt = self
            .conn
            .prepare(query)
            .map_err(|err| format!("failed to prepare asset index query: {err}"))?;
        let rows = stmt
            .query_map([], row_to_asset)
            .map_err(|err| format!("failed to read asset index: {err}"))?;
        let mut items = Vec::new();
        for row in rows {
            items.push(row.map_err(|err| format!("failed to decode asset index row: {err}"))?);
        }
        Ok(items)
    }
}

const LIST_ALL_QUERY: &str = r#"
    SELECT
        id,
        asset_type,
        source,
        root_id,
        root_label,
        name,
        absolute_path,
        relative_path,
        extension,
        size_bytes,
        modified_ms,
        sidecar_path,
        sfx_hook_id,
        sfx_runtime_hook_id,
        sfx_variant_key,
        sfx_profile_key,
        sfx_qc_status,
        sfx_qc_label,
        sfx_qc_score,
        thumbnail_path,
        structure_key,
        structure_label,
        default_visible,
        unity_guid,
        unity_usage_status,
        unity_usage_label,
        unity_usage_count,
        unity_usage_paths,
        indexed_ms
    FROM assets
    ORDER BY default_visible DESC, modified_ms DESC, relative_path ASC
"#;

const LIST_DEFAULT_QUERY: &str = r#"
    SELECT
        id,
        asset_type,
        source,
        root_id,
        root_label,
        name,
        absolute_path,
        relative_path,
        extension,
        size_bytes,
        modified_ms,
        sidecar_path,
        sfx_hook_id,
        sfx_runtime_hook_id,
        sfx_variant_key,
        sfx_profile_key,
        sfx_qc_status,
        sfx_qc_label,
        sfx_qc_score,
        thumbnail_path,
        structure_key,
        structure_label,
        default_visible,
        unity_guid,
        unity_usage_status,
        unity_usage_label,
        unity_usage_count,
        unity_usage_paths,
        indexed_ms
    FROM assets
    WHERE default_visible = 1
    ORDER BY modified_ms DESC, relative_path ASC
"#;

fn migrate(conn: &Connection) -> Result<(), String> {
    conn.execute_batch(
        r#"
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS assets (
            absolute_path TEXT PRIMARY KEY,
            id TEXT NOT NULL,
            asset_type TEXT NOT NULL,
            source TEXT NOT NULL,
            root_id TEXT NOT NULL,
            root_label TEXT NOT NULL,
            name TEXT NOT NULL,
            relative_path TEXT NOT NULL,
            extension TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            modified_ms INTEGER NOT NULL,
            sidecar_path TEXT,
            sfx_hook_id TEXT,
            sfx_runtime_hook_id TEXT,
            sfx_variant_key TEXT,
            sfx_profile_key TEXT,
            sfx_qc_status TEXT,
            sfx_qc_label TEXT,
            sfx_qc_score REAL,
            thumbnail_path TEXT,
            structure_key TEXT NOT NULL,
            structure_label TEXT NOT NULL,
            default_visible INTEGER NOT NULL,
            unity_guid TEXT,
            unity_usage_status TEXT NOT NULL DEFAULT 'unknown',
            unity_usage_label TEXT NOT NULL DEFAULT 'Unknown',
            unity_usage_count INTEGER NOT NULL DEFAULT 0,
            unity_usage_paths TEXT NOT NULL DEFAULT '[]',
            indexed_ms INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_assets_default_modified
            ON assets(default_visible, modified_ms DESC);
        CREATE INDEX IF NOT EXISTS idx_assets_type
            ON assets(asset_type);
        CREATE INDEX IF NOT EXISTS idx_assets_structure
            ON assets(structure_key);
        "#,
    )
    .map_err(|err| format!("failed to migrate asset index: {err}"))?;

    ensure_columns(
        conn,
        VecDeque::from([
            ("unity_guid", "TEXT"),
            ("unity_usage_status", "TEXT NOT NULL DEFAULT 'unknown'"),
            ("unity_usage_label", "TEXT NOT NULL DEFAULT 'Unknown'"),
            ("unity_usage_count", "INTEGER NOT NULL DEFAULT 0"),
            ("unity_usage_paths", "TEXT NOT NULL DEFAULT '[]'"),
            ("sfx_hook_id", "TEXT"),
            ("sfx_runtime_hook_id", "TEXT"),
            ("sfx_variant_key", "TEXT"),
            ("sfx_profile_key", "TEXT"),
            ("sfx_qc_status", "TEXT"),
            ("sfx_qc_label", "TEXT"),
            ("sfx_qc_score", "REAL"),
        ]),
    )?;

    conn.execute_batch(
        r#"
        CREATE INDEX IF NOT EXISTS idx_assets_unity_usage
            ON assets(unity_usage_status);
        CREATE INDEX IF NOT EXISTS idx_assets_sfx_qc
            ON assets(sfx_qc_status);
        "#,
    )
    .map_err(|err| format!("failed to migrate asset usage index: {err}"))
}

fn ensure_columns(
    conn: &Connection,
    columns: VecDeque<(&'static str, &'static str)>,
) -> Result<(), String> {
    let mut existing = HashSet::new();
    let mut stmt = conn
        .prepare("PRAGMA table_info(assets)")
        .map_err(|err| format!("failed to inspect asset index columns: {err}"))?;
    let rows = stmt
        .query_map([], |row| row.get::<_, String>(1))
        .map_err(|err| format!("failed to read asset index columns: {err}"))?;
    for row in rows {
        existing.insert(row.map_err(|err| format!("failed to decode asset column: {err}"))?);
    }
    drop(stmt);

    for (name, definition) in columns {
        if existing.contains(name) {
            continue;
        }
        conn.execute_batch(&format!(
            "ALTER TABLE assets ADD COLUMN {name} {definition};"
        ))
        .map_err(|err| format!("failed to add asset index column {name}: {err}"))?;
    }
    Ok(())
}

fn build_thumbnail(
    cache: &ThumbnailCache,
    path: &Path,
    modified_ms: u64,
    size_bytes: u64,
) -> Option<String> {
    is_image_file(path)
        .then(|| cache.cached_thumbnail_for(path, modified_ms, size_bytes))
        .flatten()
}

fn find_existing(
    tx: &Transaction<'_>,
    absolute_path: &str,
) -> Result<Option<ExistingAsset>, String> {
    tx.query_row(
        "SELECT size_bytes, modified_ms, indexed_ms, thumbnail_path FROM assets WHERE absolute_path = ?1",
        params![absolute_path],
        |row| {
            Ok(ExistingAsset {
                size_bytes: row.get::<_, i64>(0)? as u64,
                modified_ms: row.get::<_, i64>(1)? as u64,
                indexed_ms: row.get::<_, i64>(2)? as u64,
                thumbnail_path: row.get(3)?,
            })
        },
    )
    .optional()
    .map_err(|err| format!("failed to query existing indexed asset: {err}"))
}

fn upsert(tx: &Transaction<'_>, item: &AssetItem) -> Result<(), String> {
    let usage_paths = serde_json::to_string(&item.unity_usage_paths)
        .map_err(|err| format!("failed to encode Unity usage paths: {err}"))?;
    tx.execute(
        r#"
        INSERT INTO assets (
            absolute_path,
            id,
            asset_type,
            source,
            root_id,
            root_label,
            name,
            relative_path,
            extension,
            size_bytes,
            modified_ms,
            sidecar_path,
            sfx_hook_id,
            sfx_runtime_hook_id,
            sfx_variant_key,
            sfx_profile_key,
            sfx_qc_status,
            sfx_qc_label,
            sfx_qc_score,
            thumbnail_path,
            structure_key,
            structure_label,
            default_visible,
            unity_guid,
            unity_usage_status,
            unity_usage_label,
            unity_usage_count,
            unity_usage_paths,
            indexed_ms
        )
        VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20, ?21, ?22, ?23, ?24, ?25, ?26, ?27, ?28, ?29)
        ON CONFLICT(absolute_path) DO UPDATE SET
            id = excluded.id,
            asset_type = excluded.asset_type,
            source = excluded.source,
            root_id = excluded.root_id,
            root_label = excluded.root_label,
            name = excluded.name,
            relative_path = excluded.relative_path,
            extension = excluded.extension,
            size_bytes = excluded.size_bytes,
            modified_ms = excluded.modified_ms,
            sidecar_path = excluded.sidecar_path,
            sfx_hook_id = excluded.sfx_hook_id,
            sfx_runtime_hook_id = excluded.sfx_runtime_hook_id,
            sfx_variant_key = excluded.sfx_variant_key,
            sfx_profile_key = excluded.sfx_profile_key,
            sfx_qc_status = excluded.sfx_qc_status,
            sfx_qc_label = excluded.sfx_qc_label,
            sfx_qc_score = excluded.sfx_qc_score,
            thumbnail_path = excluded.thumbnail_path,
            structure_key = excluded.structure_key,
            structure_label = excluded.structure_label,
            default_visible = excluded.default_visible,
            unity_guid = excluded.unity_guid,
            unity_usage_status = excluded.unity_usage_status,
            unity_usage_label = excluded.unity_usage_label,
            unity_usage_count = excluded.unity_usage_count,
            unity_usage_paths = excluded.unity_usage_paths,
            indexed_ms = excluded.indexed_ms
        "#,
        params![
            &item.absolute_path,
            &item.id,
            &item.asset_type,
            &item.source,
            &item.root_id,
            &item.root_label,
            &item.name,
            &item.relative_path,
            &item.extension,
            item.size_bytes as i64,
            item.modified_ms as i64,
            item.sidecar_path.as_deref(),
            item.sfx_hook_id.as_deref(),
            item.sfx_runtime_hook_id.as_deref(),
            item.sfx_variant_key.as_deref(),
            item.sfx_profile_key.as_deref(),
            item.sfx_qc_status.as_deref(),
            item.sfx_qc_label.as_deref(),
            item.sfx_qc_score,
            item.thumbnail_path.as_deref(),
            &item.structure_key,
            &item.structure_label,
            if item.default_visible { 1 } else { 0 },
            item.unity_guid.as_deref(),
            &item.unity_usage_status,
            &item.unity_usage_label,
            item.unity_usage_count as i64,
            usage_paths,
            item.indexed_ms as i64,
        ],
    )
    .map(|_| ())
    .map_err(|err| {
        format!(
            "failed to upsert indexed asset {}: {err}",
            item.absolute_path
        )
    })
}

fn delete_missing(
    tx: &Transaction<'_>,
    root_id: &str,
    seen: &HashSet<String>,
) -> Result<(), String> {
    let mut stmt = tx
        .prepare("SELECT absolute_path FROM assets WHERE root_id = ?1")
        .map_err(|err| format!("failed to prepare stale asset query: {err}"))?;
    let paths = stmt
        .query_map(params![root_id], |row| row.get::<_, String>(0))
        .map_err(|err| format!("failed to read stale asset candidates: {err}"))?;
    let mut stale_paths = Vec::new();
    for path in paths {
        let path = path.map_err(|err| format!("failed to decode stale asset path: {err}"))?;
        if !seen.contains(&path) {
            stale_paths.push(path);
        }
    }
    drop(stmt);

    for path in stale_paths {
        tx.execute("DELETE FROM assets WHERE absolute_path = ?1", params![path])
            .map_err(|err| format!("failed to delete stale indexed asset: {err}"))?;
    }

    Ok(())
}

fn row_to_asset(row: &rusqlite::Row<'_>) -> rusqlite::Result<AssetItem> {
    let usage_paths_text: String = row.get(27)?;
    let usage_paths = serde_json::from_str::<Vec<String>>(&usage_paths_text).unwrap_or_default();
    Ok(AssetItem {
        id: row.get(0)?,
        asset_type: row.get(1)?,
        source: row.get(2)?,
        root_id: row.get(3)?,
        root_label: row.get(4)?,
        name: row.get(5)?,
        absolute_path: row.get(6)?,
        relative_path: row.get(7)?,
        extension: row.get(8)?,
        size_bytes: row.get::<_, i64>(9)? as u64,
        modified_ms: row.get::<_, i64>(10)? as u64,
        sidecar_path: row.get(11)?,
        sfx_hook_id: row.get(12)?,
        sfx_runtime_hook_id: row.get(13)?,
        sfx_variant_key: row.get(14)?,
        sfx_profile_key: row.get(15)?,
        sfx_qc_status: row.get(16)?,
        sfx_qc_label: row.get(17)?,
        sfx_qc_score: row.get(18)?,
        thumbnail_path: row.get(19)?,
        structure_key: row.get(20)?,
        structure_label: row.get(21)?,
        default_visible: row.get::<_, i64>(22)? != 0,
        unity_guid: row.get(23)?,
        unity_usage_status: row.get(24)?,
        unity_usage_label: row.get(25)?,
        unity_usage_count: row.get::<_, i64>(26)? as usize,
        unity_usage_paths: usage_paths,
        indexed_ms: row.get::<_, i64>(28)? as u64,
    })
}

fn now_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_millis() as u64)
        .unwrap_or(0)
}
