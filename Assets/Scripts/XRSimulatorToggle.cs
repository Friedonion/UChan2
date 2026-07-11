using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

public class XRSimulatorToggle : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return null; // XR 서브시스템 초기화 대기

        var headDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
        if (headDevices.Count > 0)
            gameObject.SetActive(false);
    }
}
