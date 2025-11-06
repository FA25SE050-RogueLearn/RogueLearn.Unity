using UnityEngine;
using BossFight2D.Systems;
using System;
using UnityEngine.SceneManagement;

namespace BossFight2D.Core
{
  public enum GameState { Init, Playing, Win, Lose, Paused }
  public class GameManager : MonoBehaviour
  {
    public GameState State = GameState.Init;
    void Awake()
    {
      DontDestroyOnLoad(gameObject);
      SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
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
      Systems.EventBus.RaiseGameWon();
    }
    public void LoseGame() { State = GameState.Lose; Systems.EventBus.RaiseGameLost(); }
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