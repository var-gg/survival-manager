use std::time::Duration;

use serde::{Deserialize, Serialize};

use crate::config::load_config;

const CHATTERBOX_ENGINE_ID: &str = "chatterbox";

/// Request coming from the webview. Field names are camelCase on the JS side and
/// map onto the snake_case body that the ai-infra Chatterbox server expects.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GenerateVoiceRequest {
    pub text: String,
    #[serde(default = "default_engine")]
    pub engine: String,
    #[serde(default)]
    pub language: Option<String>,
    #[serde(default)]
    pub voice_id: Option<String>,
    #[serde(default)]
    pub reference_audio_path: Option<String>,
    #[serde(default)]
    pub output_name: Option<String>,
    #[serde(default)]
    pub seed: Option<i64>,
    #[serde(default)]
    pub exaggeration: Option<f32>,
    #[serde(default)]
    pub cfg_weight: Option<f32>,
}

fn default_engine() -> String {
    "multilingual".to_string()
}

/// Result handed back to the webview (camelCase) so it can play the wav.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GenerateVoiceOutput {
    pub id: String,
    pub audio_url: String,
    pub output_path: String,
    pub engine: String,
    pub model_id: String,
    pub audio_duration: f64,
}

#[derive(Debug, Default, Deserialize)]
struct ChatterboxOutput {
    #[serde(default)]
    id: String,
    #[serde(default)]
    audio_url: String,
    #[serde(default)]
    output_path: String,
    #[serde(default)]
    engine: String,
    #[serde(default)]
    model_id: String,
    #[serde(default)]
    audio_duration: f64,
}

#[derive(Debug, Deserialize)]
struct ChatterboxResponse {
    #[serde(default)]
    output: ChatterboxOutput,
}

/// POST to the configured Chatterbox engine `/generate` and return the produced
/// wav location. This app never loads a model; it only drives the ai-infra HTTP
/// engine, mirroring the existing health/start command surface.
pub async fn generate(request: GenerateVoiceRequest) -> Result<GenerateVoiceOutput, String> {
    if request.text.trim().is_empty() {
        return Err("text is empty".to_string());
    }

    let config = load_config()?;
    let engine = config
        .engines
        .iter()
        .find(|engine| engine.id == CHATTERBOX_ENGINE_ID)
        .ok_or_else(|| "chatterbox engine is not configured".to_string())?;
    let base_url = engine
        .base_url
        .as_ref()
        .ok_or_else(|| "chatterbox base_url is not configured".to_string())?;
    let url = format!("{}/generate", base_url.trim_end_matches('/'));

    let mut body = serde_json::Map::new();
    body.insert("text".to_string(), serde_json::json!(request.text));
    let engine_name = if request.engine.trim().is_empty() {
        default_engine()
    } else {
        request.engine.clone()
    };
    body.insert("engine".to_string(), serde_json::json!(engine_name));
    if let Some(language) = request.language.as_ref().filter(|value| !value.trim().is_empty()) {
        body.insert("language".to_string(), serde_json::json!(language));
    }
    if let Some(voice_id) = request.voice_id.as_ref().filter(|value| !value.trim().is_empty()) {
        body.insert("voice_id".to_string(), serde_json::json!(voice_id));
    }
    if let Some(reference) = request
        .reference_audio_path
        .as_ref()
        .filter(|value| !value.trim().is_empty())
    {
        body.insert(
            "reference_audio_path".to_string(),
            serde_json::json!(reference),
        );
    }
    if let Some(output_name) = request
        .output_name
        .as_ref()
        .filter(|value| !value.trim().is_empty())
    {
        body.insert("output_name".to_string(), serde_json::json!(output_name));
    }
    if let Some(seed) = request.seed {
        body.insert("seed".to_string(), serde_json::json!(seed.max(0)));
    }
    if let Some(exaggeration) = request.exaggeration {
        body.insert("exaggeration".to_string(), serde_json::json!(exaggeration));
    }
    if let Some(cfg_weight) = request.cfg_weight {
        body.insert("cfg_weight".to_string(), serde_json::json!(cfg_weight));
    }

    let client = reqwest::Client::builder()
        .timeout(Duration::from_secs(240))
        .build()
        .map_err(|err| format!("failed to build HTTP client: {err}"))?;

    let response = client
        .post(&url)
        .json(&serde_json::Value::Object(body))
        .send()
        .await
        .map_err(|err| format!("chatterbox request failed ({url}): {err}"))?;

    let status = response.status();
    if !status.is_success() {
        let detail = response.text().await.unwrap_or_default();
        return Err(format!("chatterbox returned HTTP {status}: {detail}"));
    }

    let parsed = response
        .json::<ChatterboxResponse>()
        .await
        .map_err(|err| format!("failed to parse chatterbox response: {err}"))?;
    let output = parsed.output;

    Ok(GenerateVoiceOutput {
        id: output.id,
        audio_url: output.audio_url,
        output_path: output.output_path,
        engine: output.engine,
        model_id: output.model_id,
        audio_duration: output.audio_duration,
    })
}
