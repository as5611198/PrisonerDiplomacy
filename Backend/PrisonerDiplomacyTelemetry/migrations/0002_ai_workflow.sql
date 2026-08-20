ALTER TABLE error_reports ADD COLUMN triage_last_error TEXT;
ALTER TABLE error_reports ADD COLUMN triage_last_attempt_at TEXT;
ALTER TABLE error_reports ADD COLUMN triage_retry_after TEXT;
ALTER TABLE error_reports ADD COLUMN repair_status TEXT NOT NULL DEFAULT 'none';
ALTER TABLE error_reports ADD COLUMN repair_attempt_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE error_reports ADD COLUMN repair_last_attempt_at TEXT;
ALTER TABLE error_reports ADD COLUMN repair_next_attempt_at TEXT;
ALTER TABLE error_reports ADD COLUMN repair_last_error TEXT;
ALTER TABLE error_reports ADD COLUMN repair_candidate_json TEXT;
ALTER TABLE error_reports ADD COLUMN repair_candidate_r2_key TEXT;

CREATE TABLE IF NOT EXISTS ai_daily_usage (
  usage_day TEXT PRIMARY KEY NOT NULL,
  triage_count INTEGER NOT NULL DEFAULT 0,
  repair_attempt_count INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ai_attempts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  error_hash TEXT NOT NULL,
  stage TEXT NOT NULL CHECK (stage IN ('triage', 'repair')),
  attempt_number INTEGER NOT NULL,
  provider TEXT NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('started', 'succeeded', 'failed')),
  started_at TEXT NOT NULL,
  finished_at TEXT,
  next_retry_at TEXT,
  error_code TEXT,
  response_sha256 TEXT,
  result_json TEXT
);

CREATE TABLE IF NOT EXISTS ai_job_locks (
  job_key TEXT PRIMARY KEY NOT NULL,
  owner TEXT NOT NULL,
  locked_until TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_error_reports_triage_queue
  ON error_reports (status, triage_classification, occurrence_count DESC, last_seen DESC);

CREATE INDEX IF NOT EXISTS idx_error_reports_repair_queue
  ON error_reports (repair_status, repair_next_attempt_at, occurrence_count DESC);

CREATE INDEX IF NOT EXISTS idx_ai_attempts_issue
  ON ai_attempts (error_hash, stage, started_at DESC);
