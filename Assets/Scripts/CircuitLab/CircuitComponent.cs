using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LabelAlignment { Top, Bottom, Center };

public class CircuitComponent : MonoBehaviour
{
    public CircuitLab Lab;

    public bool IsPlaced { get; protected set; }
    public bool IsHeld { get; protected set; }
    public bool IsClone { get; set; }
    public bool IsClosed { get; protected set; }

    protected Point StartingPeg { get; set; }
    protected Direction Direction { get; set; }
    protected bool IsActive { get; set; }
    protected bool IsForward { get; set; }
    protected bool IsShortCircuit { get; set; }
    protected double Voltage { get; set; }
    protected double Current { get; set; }

    const double SignificantCurrent = 0.0000001;
    const float LabelOffset = 0.022f;

    protected CircuitComponent()
    {
        IsPlaced = false;
        IsHeld = false;
        IsClone = false;
        IsClosed = true;
        IsActive = false;
        IsForward = true;
        IsShortCircuit = false;
        Voltage = 0f;
        Current = 0f;
    }

    protected virtual void Start() { }
    protected virtual void Update() { }

    public void Place(Point start, Direction dir)
    {
        IsPlaced = true;
        StartingPeg = start;
        Direction = dir;
    }

    public virtual void SetActive(bool isActive, bool isForward)
    {
        IsActive = isActive;
        IsForward = isForward;
    }

    public virtual void SetShortCircuit(bool isShortCircuit, bool isForward)
    {
        IsShortCircuit = isShortCircuit;
        IsForward = isForward;
    }

    public double GetVoltage()
    {
        return Voltage;
    }

    public virtual void SetVoltage(double voltage)
    {
        Voltage = voltage;
    }

    public virtual void SetCurrent(double current)
    {
        Current = current;
    }

    protected bool IsCurrentSignificant()
    {
        return (Current > SignificantCurrent);
    }

    public virtual void Toggle()
    {
    }

    public virtual void Adjust()
    {
    }

    protected virtual void Reset()
    {
    }

    public virtual void SelectEntered()
    {
        IsHeld = true;

        GetComponent<BoxCollider>().enabled = true;
        GetComponent<SphereCollider>().enabled = true;

        if (IsPlaced)
        {
            Lab.RemoveComponent(this.gameObject, StartingPeg);

            IsPlaced = false;
        }

        Reset();
    }

    public virtual void SelectExited()
    {
        IsHeld = false;

        GetComponent<Rigidbody>().isKinematic = false;
        GetComponent<Rigidbody>().useGravity = true;
    }

    protected IEnumerator PlaySound(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        source.Stop();
        source.Play();
    }

    protected void RotateLabel(GameObject label, LabelAlignment alignment)
    {
        var rotation = label.transform.localEulerAngles;
        var position = label.transform.localPosition;

        switch (Direction)
        {
            case Direction.North:
            case Direction.East:
                rotation.z = -90f;
                position.x = alignment switch
                {
                    LabelAlignment.Top => -LabelOffset,
                    LabelAlignment.Bottom => LabelOffset,
                    _ => 0
                };
                break;
            case Direction.South:
            case Direction.West:
                rotation.z = 90f;
                position.x = alignment switch
                {
                    LabelAlignment.Top => LabelOffset,
                    LabelAlignment.Bottom => -LabelOffset,
                    _ => 0
                }; break;
            default:
                Debug.Log("Unrecognized direction!");
                break;
        }

        label.transform.localEulerAngles = rotation;
        label.transform.localPosition = position;
    }
}

public enum Direction { North, South, East, West };


