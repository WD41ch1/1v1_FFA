using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UILookPlayer : MonoBehaviour
{
    private Camera mainCamera;

    // Start is called before the first frame update
    private void Start()
    {
        mainCamera = Camera.main;
    }


    // Update is called once per frame
    void LateUpdate()
    {
        //  UIがカメラのほうに向く
        transform.LookAt(
            transform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up
        );
    }
}
