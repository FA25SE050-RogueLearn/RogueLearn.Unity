using UnityEngine;

namespace BossFight2D.Player
{
    public class PlayerFocus : MonoBehaviour
    {
        public int max = 3;
        public int current = 2;

        public bool Spend(int v = 1)
        {
            if (current >= v)
            {
                current -= v;
                return true;
            }
            return false;
        }

        public void Gain(int v = 1)
        {
            current = Mathf.Min(max, current + v);
        }
    }
}
