use std::fs;
use std::path::{Path, PathBuf};

use image::{imageops::FilterType, ImageFormat, ImageReader};

use crate::assets::stable_id;
use crate::config::path_to_string;

pub struct ThumbnailCache {
    root: PathBuf,
}

impl ThumbnailCache {
    pub fn new(cache_root: &Path) -> Result<Self, String> {
        let root = cache_root.join("thumbs");
        fs::create_dir_all(&root)
            .map_err(|err| format!("failed to create thumbnail cache {}: {err}", root.display()))?;
        Ok(Self { root })
    }

    pub fn cached_thumbnail_for(
        &self,
        source: &Path,
        modified_ms: u64,
        size_bytes: u64,
    ) -> Option<String> {
        let target = self.target_path(source, modified_ms, size_bytes);
        if target.exists() {
            return Some(path_to_string(target));
        }
        None
    }

    pub fn ensure_thumbnail_for(
        &self,
        source: &Path,
        modified_ms: u64,
        size_bytes: u64,
    ) -> Option<String> {
        let target = self.target_path(source, modified_ms, size_bytes);
        if target.exists() {
            return Some(path_to_string(target));
        }
        let image = ImageReader::open(source)
            .ok()?
            .with_guessed_format()
            .ok()?
            .decode()
            .ok()?;
        let thumbnail = image.resize(360, 260, FilterType::Triangle).to_rgb8();
        thumbnail
            .save_with_format(&target, ImageFormat::Jpeg)
            .ok()?;
        Some(path_to_string(target))
    }

    fn target_path(&self, source: &Path, modified_ms: u64, size_bytes: u64) -> PathBuf {
        let cache_key = stable_id(&format!(
            "{}:{modified_ms}:{size_bytes}",
            source.to_string_lossy()
        ));
        self.root.join(format!("{cache_key}.jpg"))
    }
}
