using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script is meant to be attached to a componenet childed to the Head Bone
/// It ensures that only a headshot counts as an elimination for design purposes.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
public class HitBox : NetworkBehaviour
{
    // Reference to controller script dragged in from root parent object
    public PlayerController controller;

    /// <summary>
    /// Method called by the object that performs the raycast
    /// </summary>
    public void GetShotHitBox()
    {
        if (controller != null) 
        {
            // Call the PlayerContoller method to handle server sided event
            controller.GetShotServerRpc();
        }
    }
}
