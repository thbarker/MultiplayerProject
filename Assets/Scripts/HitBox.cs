using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class HitBox : NetworkBehaviour
{
    public PlayerController controller;

    public void GetShotHitBox()
    {
        if (controller != null) 
        {
            controller.GetShotServerRpc();
        }
    }
}
