use std::path::PathBuf;
use std::process::Command;
use std::time::{Duration, Instant};

use serde::Serialize;

use crate::config::{AppConfig, EngineConfig};

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineHealth {
    pub id: String,
    pub name: String,
    pub status: String,
    pub message: String,
    pub latency_ms: Option<u128>,
    pub can_start: bool,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineStartResult {
    pub id: String,
    pub started: bool,
    pub message: String,
}

pub async fn check_all(config: &AppConfig) -> Vec<EngineHealth> {
    let client = reqwest::Client::builder()
        .timeout(Duration::from_secs(2))
        .build()
        .expect("reqwest client should build");
    let mut results = Vec::new();

    for engine in &config.engines {
        results.push(check_one(&client, engine).await);
    }

    results
}

pub fn start(config: &AppConfig, engine_id: &str) -> Result<EngineStartResult, String> {
    let engine = config
        .engines
        .iter()
        .find(|engine| engine.id == engine_id)
        .ok_or_else(|| format!("unknown engine: {engine_id}"))?;
    let Some(script) = &engine.start_script else {
        return Ok(EngineStartResult {
            id: engine.id.clone(),
            started: false,
            message: "This engine has no local start script.".to_string(),
        });
    };

    let script_path = PathBuf::from(script);
    if !script_path.exists() {
        return Ok(EngineStartResult {
            id: engine.id.clone(),
            started: false,
            message: format!("Missing start script: {}", script_path.display()),
        });
    }

    let mut command = Command::new("powershell");
    command.args([
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        &script_path.to_string_lossy(),
    ]);

    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }

    command
        .spawn()
        .map_err(|err| format!("failed to start {}: {err}", engine.name))?;

    Ok(EngineStartResult {
        id: engine.id.clone(),
        started: true,
        message: "Start command launched. Health may take a while to turn green.".to_string(),
    })
}

async fn check_one(client: &reqwest::Client, engine: &EngineConfig) -> EngineHealth {
    if !engine.enabled {
        return health(engine, "disabled", "Disabled in configuration.", None);
    }

    if engine.kind == "external-image" {
        return check_external_image(engine);
    }

    let Some(url) = &engine.health_url else {
        return health(engine, "unknown", "No health URL configured.", None);
    };

    let started = Instant::now();
    match client.get(url).send().await {
        Ok(response) if response.status().is_success() => health(
            engine,
            "online",
            "Health endpoint responded.",
            Some(started.elapsed().as_millis()),
        ),
        Ok(response) => health(
            engine,
            "offline",
            &format!("Health returned HTTP {}.", response.status()),
            Some(started.elapsed().as_millis()),
        ),
        Err(err) => health(engine, "offline", &err.to_string(), None),
    }
}

fn check_external_image(engine: &EngineConfig) -> EngineHealth {
    let secrets = std::env::var_os("USERPROFILE")
        .map(PathBuf::from)
        .map(|path| path.join(".claude").join("secrets.env"));
    let configured = secrets.as_ref().is_some_and(|path| path.exists());
    let message = match &secrets {
        Some(path) if configured => format!("Provider secrets file exists: {}", path.display()),
        Some(path) => format!("Provider secrets file is missing: {}", path.display()),
        None => "USERPROFILE is not set; cannot locate provider secrets.".to_string(),
    };

    EngineHealth {
        id: engine.id.clone(),
        name: engine.name.clone(),
        status: if configured {
            "configured"
        } else {
            "missing_config"
        }
        .to_string(),
        message,
        latency_ms: None,
        can_start: false,
    }
}

fn health(
    engine: &EngineConfig,
    status: &str,
    message: &str,
    latency_ms: Option<u128>,
) -> EngineHealth {
    EngineHealth {
        id: engine.id.clone(),
        name: engine.name.clone(),
        status: status.to_string(),
        message: message.to_string(),
        latency_ms,
        can_start: engine
            .start_script
            .as_ref()
            .is_some_and(|script| PathBuf::from(script).exists()),
    }
}
