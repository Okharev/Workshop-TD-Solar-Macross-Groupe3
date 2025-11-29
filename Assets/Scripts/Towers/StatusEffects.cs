using System.Collections;
using Sprite = UnityEngine.Sprite;

namespace Towers
{
    public interface IStatusEffect
    {
        void OnApply(StatusHandler host);
        void Reapply(IStatusEffect newInstance);
        void OnEnd();
        IEnumerator Process(StatusHandler host);

        Sprite GetIcon();
        float GetDurationRatio();
        int GetStackCount();
    }
}