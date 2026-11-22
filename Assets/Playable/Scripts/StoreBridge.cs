using UnityEngine;

namespace PA.TileWord
{
    /// <summary>
    /// Gọi Luna Playworks (package com.unity.playworks.upp, assembly Unity.Luna).
    /// </summary>
    public static class StoreBridge
    {
        private static bool _ended;

        public static void GameEnded()
        {
            if (_ended) return;
            _ended = true;
            Debug.Log("[Playable] GameEnded");
            Luna.Unity.LifeCycle.GameEnded();
        }

        public static void InstallFullGame()
        {
            Debug.Log("[Playable] InstallFullGame -> store");
            Luna.Unity.Playable.InstallFullGame();
        }
    }
}
