using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoOnce
{
    private bool done = false;
    public bool Try()
    {
        if (done) return false;
        done = true;
        return true;
    }
    public void Reset() => done = false;
}
