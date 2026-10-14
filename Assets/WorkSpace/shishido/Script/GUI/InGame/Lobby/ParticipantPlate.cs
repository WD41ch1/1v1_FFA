using TMPro;
using UnityEngine;

public class ParticipantPlate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerName;
    [SerializeField] private GameObject _hostPlate;
    [SerializeField] private GameObject _youPlate;
    [SerializeField] private GameObject _readyPlate;

    public ParticipantData myData { get; private set; }
    public int _playerID { get; private set; }

    public void Initialize(ParticipantData _data, bool _you)
    {
        myData = _data;

        _playerName.text = myData.PlayerName; ;
        _playerID = myData.PlayerId;
        _hostPlate.SetActive(myData.IsHost);
        _youPlate.SetActive(_you);
        SetReady(false);
    }

    public void SetReady(bool flag)
    {
        _readyPlate.SetActive(flag);
    }

    public void DisConnect()
    {
        _playerName.text = string.Empty;
        _playerID = -1;
        _hostPlate.SetActive(false);
        _youPlate.SetActive(false);

        transform.gameObject.SetActive(false);
    }
}
