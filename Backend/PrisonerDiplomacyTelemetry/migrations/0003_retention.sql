ALTER TABLE error_reports ADD COLUMN last_received_at TEXT;

UPDATE error_reports
   SET last_received_at = updated_at
 WHERE last_received_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_error_report_events_retention
  ON error_report_events (received_at);

CREATE INDEX IF NOT EXISTS idx_error_reports_retention
  ON error_reports (last_received_at);

CREATE INDEX IF NOT EXISTS idx_ai_attempts_retention
  ON ai_attempts (started_at);
