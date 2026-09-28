using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class alphaSceneManager : MonoBehaviour
{
    [SerializeField]
    public PlayerManager[] players;

    public List<PlayerManager> ranking = new List<PlayerManager>();


    [SerializeField] private GameObject resultCanvasPrefab;

    private bool onece = false;

    void Start()
    {
        //  すべてのPlayer(PlayerManager)を取得
        players = FindObjectsByType<PlayerManager>(
            FindObjectsSortMode.None
        );


    }

    // Update is called once per frame
    void Update()
    {
        if (clearCheck() && !onece)
        {
            // 最後まで生き残ったPlayerをrankingに追加
            foreach (var player in players)
            {
                if (!player.playerHealth.IsDead)
                {
                    if (!ranking.Contains(player))
                        ranking.Add(player);
                }
            }

            GameObject canvas = Instantiate(resultCanvasPrefab);
            canvas.GetComponent<ResultCavasManager>()?.Initializa(ranking);
            Time.timeScale = 0;

            onece = true;
        }

        CreateRanking();
    }

    private bool clearCheck()
    {
        int aliveCounter = 0;

        foreach (var player in players)
        {
            if (!player.playerHealth.IsDead)
            {
                aliveCounter++;
            }
        }

        // 生存者が1人になったら終了
        return aliveCounter == 1;
    }

    private void CreateRanking()
    {
        foreach (var player in players)
        {
            if (player.playerHealth.IsDead)
            {
                if (!ranking.Contains(player))
                    ranking.Add(player);
            }
        }
    }
}
