using UnityEngine;

public class PersistentLIV : MonoBehaviour
{
    // [Removed DontDestroyOnLoad logic]
    // 
    // Explanation:
    // Making LIV persist across scenes while the XR Origin (Camera Rig) is destroyed 
    // and recreated causes the LIV PC Compositor to crash (as seen in the error).
    // The safest and officially recommended approach when the XR Rig is not persistent 
    // is to allow the LIV prefab in each scene to initialize itself normally.
}
