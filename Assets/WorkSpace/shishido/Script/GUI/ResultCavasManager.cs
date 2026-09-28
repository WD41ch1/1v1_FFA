using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;

public class ResultCavasManager : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab;
    [SerializeField] private GameObject rankingOBJ;


    public List<PlayerManager> ranking;

    void Start()
    {

    }

    public void Initializa(List<PlayerManager> _ranking)
    {
        ranking = _ranking;

        ShowRanking();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ShowRanking()
    {
        if (ranking == null) return;

        ranking.Reverse();

        for (int i = 0; i < ranking.Count; i++) {

            CreateText(rankingOBJ, $"#{i + 1}|{ranking[i].name}");
        }

    }


    public void CreateText(GameObject target, string text)
    {
        GameObject obj = Instantiate(
            textPrefab,
            target.transform
        );

        obj.transform.localPosition = new Vector3(0, 2f, 0);

        TextMeshProUGUI tmp = obj.GetComponentInChildren<TextMeshProUGUI>();

        if (tmp != null)
        {
            tmp.text = text;
        }
    }


    public void GameQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
