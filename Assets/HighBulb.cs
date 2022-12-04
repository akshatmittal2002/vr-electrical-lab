using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HighBulb : CircuitComponent, IResistor
{
    public GameObject labelResistance;
    public TMP_Text labelResistanceText;
    public GameObject labelCurrent;
    public TMP_Text labelCurrentText;
    public GameObject filament;
    public AudioSource colorChangeAudio;

    float intensity = 0f;

    bool cooldownActive = false;
    Color[] colors = { Color.red, Color.yellow, Color.green, Color.blue, Color.magenta };
    int emissionColorIdx = 4;

    public float Resistance { get; private set; }

    public HighBulb()
    {
        Resistance = 5000f;
    }

    protected override void Update()
    {
        // Show/hide the labels
        labelResistance.gameObject.SetActive(IsActive && IsCurrentSignificant() && Lab.showLabels);
        labelCurrent.gameObject.SetActive(IsActive && IsCurrentSignificant() && Lab.showLabels);
    }

    public override void SetActive(bool isActive, bool isForward)
    {
        IsActive = isActive;

        if (!isActive)
            DeactivateLight();

        labelResistanceText.text = Resistance.ToString("0.#") + "Ω";

        RotateLabel(labelResistance, LabelAlignment.Top);
        RotateLabel(labelCurrent, LabelAlignment.Bottom);
    }

    private void DeactivateLight()
    {
        filament.GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
    }

    private void ActivateLight()
    {
        filament.GetComponent<Renderer>().material.EnableKeyword("_EMISSION");

        Color baseColor = colors[emissionColorIdx];
        Color finalColor = baseColor * Mathf.Pow(2, intensity);
        filament.GetComponent<Renderer>().material.SetColor("_EmissionColor", finalColor);
    }

    public override void SetCurrent(double current)
    {
        Current = current;

        if (!IsCurrentSignificant())
        {
            IsActive = false;
            DeactivateLight();
        }
        else
        {
            labelCurrentText.text = (current * 1000f).ToString("0.#") + "mA";

            float maxCurrent = 0.01f;
            float maxIntensity = 5.0f;
            float minIntensity = 3.0f;
            float pctCurrent = ((float)current > maxCurrent ? maxCurrent : (float)current) / maxCurrent;
            intensity = (pctCurrent * (maxIntensity - minIntensity)) + minIntensity;

            ActivateLight();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!cooldownActive && IsActive &&
            other.gameObject.name.Contains("Pinch"))
        {
            emissionColorIdx = ++emissionColorIdx % colors.Length;
            ActivateLight();

            StartCoroutine(PlaySound(colorChangeAudio, 0f));

            cooldownActive = true;
            Invoke("Cooldown", 0.5f);
        }
    }

    void Cooldown()
    {
        cooldownActive = false;
    }
}
