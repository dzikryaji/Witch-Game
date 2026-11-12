using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Domain.Progression;

namespace Witch.Presentation.Rewind
{
    public sealed class RewindHeaderView : MonoBehaviour
    {
        [SerializeField] private Text moneyText;
        [SerializeField] private Text dayText;
        [SerializeField] private RectTransform suspicionFill;
        [SerializeField] private Text suspicionText;
        [SerializeField] private string moneyFormat = "${0}";
        [SerializeField] private string dayFormat = "Day {0}/{1}";
        [SerializeField, Min(1)] private int totalDays = 14;

        private Coroutine drainRoutine;

        public void Render(GameSession session)
        {
            if (session == null) return;

            int suspicion = session.RunState.Suspicion;
            int threshold = RunState.RewindSuspicionThreshold;

            if (moneyText != null) moneyText.text = string.Format(moneyFormat, session.RunState.Money);
            if (dayText != null) dayText.text = string.Format(dayFormat, session.RunState.CurrentDayNumber, totalDays);
            if (suspicionText != null) suspicionText.text = suspicion + "/" + threshold;
            SetFill(Mathf.Clamp01(suspicion / (float)threshold));
        }
        
        public void DrainSuspicion(float seconds)
        {
            if (suspicionFill == null) return;
            if (drainRoutine != null) StopCoroutine(drainRoutine);
            drainRoutine = StartCoroutine(Drain(seconds));
        }

        private IEnumerator Drain(float seconds)
        {
            float start = suspicionFill.anchorMax.x;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFill(Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            SetFill(0f);
            if (suspicionText != null) suspicionText.text = "0/" + RunState.RewindSuspicionThreshold;
            drainRoutine = null;
        }

        private void SetFill(float fraction)
        {
            if (suspicionFill == null) return;
            suspicionFill.anchorMax = new Vector2(fraction, suspicionFill.anchorMax.y);
        }
    }
}