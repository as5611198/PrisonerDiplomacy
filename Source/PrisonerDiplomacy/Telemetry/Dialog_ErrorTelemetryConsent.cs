using System;
using UnityEngine;
using Verse;

namespace PrisonerDiplomacy.Telemetry
{
    internal enum ErrorTelemetryConsentDecision
    {
        AllowOnce,
        AllowSession,
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

        public override Vector2 InitialSize => new Vector2(720f, 250f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f),
                "PD_ErrorTelemetryDialogTitle".Translate());
            Text.Font = GameFont.Small;

            Rect messageRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, 86f);
            Widgets.Label(messageRect, "PD_ErrorTelemetryDialogText".Translate());

            const float gap = 10f;
            float buttonWidth = (inRect.width - gap * 2f) / 3f;
            float buttonY = inRect.yMax - 42f;
            if (Widgets.ButtonText(new Rect(inRect.x, buttonY, buttonWidth, 38f),
                "PD_ErrorTelemetryAllowOnce".Translate()))
            {
                Resolve(ErrorTelemetryConsentDecision.AllowOnce);
            }
            if (Widgets.ButtonText(new Rect(inRect.x + buttonWidth + gap, buttonY, buttonWidth, 38f),
                "PD_ErrorTelemetryAllowSession".Translate()))
            {
                Resolve(ErrorTelemetryConsentDecision.AllowSession);
            }
            if (Widgets.ButtonText(new Rect(inRect.x + (buttonWidth + gap) * 2f, buttonY, buttonWidth, 38f),
                "PD_ErrorTelemetryReject".Translate()))
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
