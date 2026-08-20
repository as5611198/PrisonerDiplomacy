CREATE TABLE IF NOT EXISTS error_reports (
  hash TEXT PRIMARY KEY NOT NULL,
  error_message TEXT NOT NULL,
  exception_type TEXT NOT NULL,
  operation TEXT NOT NULL,
  source TEXT NOT NULL,
  trust_level TEXT NOT NULL,
  mod_version TEXT NOT NULL,
  game_version TEXT NOT NULL,
  occurrence_count INTEGER NOT NULL DEFAULT 0,
  first_seen TEXT NOT NULL,
  last_seen TEXT NOT NULL,
  r2_log_key TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'analyzing', 'fix_candidate', 'needs_repro', 'resolved', 'ignored')),
  triage_classification TEXT,
  triage_confidence REAL,
  triage_severity TEXT,
  triage_evidence_json TEXT,
  triage_provider TEXT,
  triaged_at TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS error_report_events (
  event_id TEXT PRIMARY KEY NOT NULL,
  error_hash TEXT NOT NULL,
  captured_at_utc TEXT NOT NULL,
  received_at TEXT NOT NULL,
  r2_log_key TEXT NOT NULL,
  payload_sha256 TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ingest_rate_limits (
  bucket_start INTEGER NOT NULL,
  ip_hash TEXT NOT NULL,
  request_count INTEGER NOT NULL DEFAULT 0,
  expires_at INTEGER NOT NULL,
  PRIMARY KEY (bucket_start, ip_hash)
);

CREATE INDEX IF NOT EXISTS idx_error_reports_pending_top
  ON error_reports (status, occurrence_count DESC, last_seen DESC);

CREATE INDEX IF NOT EXISTS idx_error_report_events_hash
  ON error_report_events (error_hash, captured_at_utc DESC);

CREATE INDEX IF NOT EXISTS idx_ingest_rate_limits_expiry
  ON ingest_rate_limits (expires_at);
