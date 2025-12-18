using UnityEngine;
using BossFight2D.Systems;
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using BossFight2D.Network;

namespace BossFight2D.Core
{
  public enum GameState { Init, Playing, Win, Lose, Paused }
  public class GameManager : MonoBehaviour
  {
    public static GameManager Instance { get; private set; }
    public GameState State = GameState.Init;
    void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(gameObject);
        return;
      }

      Instance = this;
      DontDestroyOnLoad(gameObject);
      SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
      if (Instance == this) Instance = null;
      SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      // Reset the game state when a new scene is loaded
      State = GameState.Init;
      Time.timeScale = 1f;
      // Ensure any components that froze on pause resume control after scene transitions
      Systems.EventBus.RaiseGameResumed();
    }

    public void StartGame() { State = GameState.Playing; Systems.EventBus.RaiseGameStarted(); }
    public void WinGame()
    {
      if (State == GameState.Win) return;
      State = GameState.Win;

      var nm = NetworkManager.Singleton;
      if (nm != null && nm.IsServer)
      {
        NetworkGameState.ServerSetWin();
        return;
      }

      Systems.EventBus.RaiseGameWon();
    }
    public void LoseGame()
    {
      if (State == GameState.Win || State == GameState.Lose) return;
      State = GameState.Lose;

      var nm = NetworkManager.Singleton;
      if (nm != null && nm.IsServer)
      {
        NetworkGameState.ServerSetLose();
        return;
      }

      Systems.EventBus.RaiseGameLost();
    }
    public void PauseGame()
    {
      // Allow pausing from Init or Playing (block only in Win/Lose/Paused)
      if (State != GameState.Win && State != GameState.Lose && State != GameState.Paused)
      {
        State = GameState.Paused; Time.timeScale = 0f; Systems.EventBus.RaiseGamePaused();
      }
    }
    public void ResumeGame() { if (State == GameState.Paused) { State = GameState.Playing; Time.timeScale = 1f; Systems.EventBus.RaiseGameResumed(); } }
    public void Restart() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
  }
}
