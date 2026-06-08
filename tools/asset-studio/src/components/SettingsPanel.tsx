import { Settings } from "lucide-react";

import { compactPath } from "../lib/format";
import type { AppConfig } from "../types";

interface Props {
  config: AppConfig | null;
}

export function SettingsPanel({ config }: Props) {
  if (!config) {
    return null;
  }

  return (
    <section className="settings-panel">
      <div className="settings-panel__title">
        <Settings size={17} />
        <span>Configuration</span>
      </div>
      <div className="settings-grid">
        <span>Project</span>
        <code>{compactPath(config.projectRoot)}</code>
        <span>AI Infra</span>
        <code>{compactPath(config.aiInfraRoot)}</code>
        <span>Cache</span>
        <code>{compactPath(config.cacheRoot)}</code>
        <span>Config</span>
        <code>{compactPath(config.configSource)}</code>
      </div>
    </section>
  );
}
