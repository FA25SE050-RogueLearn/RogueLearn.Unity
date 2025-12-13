using Unity.Netcode.Components;
using UnityEngine;

namespace BossFight2D.Network
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}