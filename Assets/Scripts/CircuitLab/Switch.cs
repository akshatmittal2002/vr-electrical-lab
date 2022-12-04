using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Switch : CircuitComponent, IConductor
{
    public GameObject pivot;
    public GameObject labelVoltage;
    public TMP_Text labelVoltageText;
    public GameObject labelCurrent;
    public TMP_Text labelCurrentText;
    public AudioSource switchSound;

    public Switch()
    {
        IsClosed = false;
    }

    protected override void Update ()
    {
        bool showLabels = Lab.showLabels && IsActive && IsCurrentSignificant() && !IsShortCircuit;
        labelVoltage.gameObject.SetActive(showLabels);
        labelCurrent.gameObject.SetActive(showLabels);
    }

    public override void SetActive(bool isActive, bool isForward)
    {
        IsActive = isActive;

        RotateLabel(labelVoltage, LabelAlignment.Top);
        RotateLabel(labelCurrent, LabelAlignment.Bottom);
    }

    public override void SetShortCircuit(bool isShortCircuit, bool isForward)
    {
        IsShortCircuit = isShortCircuit;
    }

    public override void Toggle()
    {
        IsClosed = !IsClosed;

        var rotation = pivot.transform.localEulerAngles;
        rotation.z = IsClosed ? 0 : -45f;
        pivot.transform.localEulerAngles = rotation;

        StartCoroutine(PlaySound(switchSound, 0f));

        Lab.SimulateCircuit();
    }

    public override void SetVoltage(double voltage)
    {
        Voltage = voltage;

        labelVoltageText.text = voltage.ToString("0.#") + "V";
    }

    public override void SetCurrent(double current)
    {
        Current = current;

        labelCurrentText.text = (current * 1000f).ToString("0.#") + "mA";
    }
}
