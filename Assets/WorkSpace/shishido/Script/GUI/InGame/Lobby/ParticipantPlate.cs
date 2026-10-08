using TMPro;
using UnityEngine;

public class ParticipantPlate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerName;
    [SerializeField] private GameObject _hostPlate;
    [SerializeField] private GameObject _youPlate;

    public void Initialize(string _name,int _id,bool _isHost = false,bool _you = false)
    {
        _playerName.text = _name;
        _hostPlate.SetActive(_isHost);
        _youPlate.SetActive(_you);
    }

}
