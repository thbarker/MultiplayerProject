using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script is designed to ensure Cameras a client specific
/// and not globally shared
/// </summary>
public class CameraController : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener listener;

    void Start()
    {
        // Only enable the camera if this is the local player
        if (IsOwner)
        {
            playerCamera.enabled = true;
            listener.enabled = true;
        }
        else
        {
            playerCamera.enabled = false;
            listener.enabled = false;
        }
    }
}
