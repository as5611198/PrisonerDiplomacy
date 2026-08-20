using System;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy.Telemetry
{
    internal enum ErrorTelemetryConsentDecision
    {
        AllowOnce,
        AllowSession,
        AllowAlways,
        Reject
    }

    internal sealed class Dialog_ErrorTelemetryConsent : Window
    {
        private readonly Action<ErrorTelemetryConsentDecision> decisionCallback;
        private bool resolved;

        public Dialog_ErrorTelemetryConsent(Action<ErrorTelemetryConsentDecision> decisionCallback)
        {
            this.decisionCallback = decisionCallback;
            doCloseX = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(720f, 330f);

        public override void DoWindowContents(Rect inRect)
        {
            PrisonerDiplomacyUiTheme.ResetText();
            Widgets.DrawBoxSolid(inRect, PrisonerDiplomacyUiTheme.Canvas);

            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 56f);
            PrisonerDiplomacyUiTheme.DrawPanel(headerRect, true);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(headerRect.ContractedBy(16f, 6f),
                "PD_ErrorTelemetryDialogTitle".Translate());
            PrisonerDiplomacyUiTheme.ResetText();

            Rect messageRect = new Rect(inRect.x, headerRect.yMax + 10f, inRect.width, 96f);
            PrisonerDiplomacyUiTheme.DrawNotice(messageRect, DiplomacyUiTone.Neutral);
            GUI.color = PrisonerDiplomacyUiTheme.TextMuted;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(messageRect.ContractedBy(16f, 10f), "PD_ErrorTelemetryDialogText".Translate());
            PrisonerDiplomacyUiTheme.ResetText();

            const float gap = 10f;
            const float buttonHeight = 42f;
            float buttonWidth = (inRect.width - gap) * 0.5f;
            float firstRowY = inRect.yMax - buttonHeight * 2f - gap;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.x, firstRowY, buttonWidth, buttonHeight),
                "PD_ErrorTelemetryAllowOnce".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Resolve(ErrorTelemetryConsentDecision.AllowOnce);
            }
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.x + buttonWidth + gap, firstRowY, buttonWidth, buttonHeight),
                "PD_ErrorTelemetryAllowSession".Translate(),
                DiplomacyUiButtonStyle.Secondary))
            {
                Resolve(ErrorTelemetryConsentDecision.AllowSession);
            }
            float secondRowY = firstRowY + buttonHeight + gap;
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.x, secondRowY, buttonWidth, buttonHeight),
                "PD_ErrorTelemetryAllowAlways".Translate(),
                DiplomacyUiButtonStyle.Primary))
            {
                Resolve(ErrorTelemetryConsentDecision.AllowAlways);
            }
            if (PrisonerDiplomacyUiTheme.DrawButton(
                new Rect(inRect.x + buttonWidth + gap, secondRowY, buttonWidth, buttonHeight),
                "PD_ErrorTelemetryReject".Translate(),
                DiplomacyUiButtonStyle.Danger))
            {
                Resolve(ErrorTelemetryConsentDecision.Reject);
            }
        }

        public override void OnCancelKeyPressed()
        {
            Resolve(ErrorTelemetryConsentDecision.Reject);
        }

        private void Resolve(ErrorTelemetryConsentDecision decision)
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            Close(false);
            decisionCallback?.Invoke(decision);
        }
    }
}
