using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using UnityEngine.XR.Interaction.Toolkit;

public class CircuitLab : MonoBehaviour, ICircuitLab
{
    public AudioSource circuitSound;
    public AudioSource shortSound1;
    public AudioSource shortSound2;
    public float circuitSoundStartTime = 0f;
    public float shortSound1StartTime = 0f;
    public float shortSound2StartTime = 0f;
    public float fadeDelay = 2f;
    public float fadeTime = 2;
    public GameObject handle;
    public bool showLabels = false;
    public GameObject pegTemplate = null;
    public float pegInterval = 0.1f;
    public float pegHeight = 0.45f;
    public Vector3 pegScale;

    Board board;
    const int numRows = 9;
    const int numCols = 9;

    float yHandleStart = 0f;
    float yTableStart = 0f;

    List<IDynamic> dynamicComponents = new List<IDynamic>();
    int numActiveCircuits = 0;

    void Start()
    {
        yHandleStart = handle.transform.position.y;
        yTableStart = transform.localPosition.y;

        board = new Board(numRows, numCols);

        CreatePegs();
        PreloadSimulator();
    }

    void PreloadSimulator()
    {
        var ckt = new Circuit(
            new VoltageSource("V1", "in", "0", 1.0),
            new Resistor("R1", "in", "out", 1.0e4),
            new Resistor("R2", "out", "0", 2.0e4)
            );
        var dc = new OP("DC 1");
        dc.Run(ckt);
    }

    void Update()
    {
        float yHandleCurrent = handle.transform.position.y;
        // every frame use the position of handle to set position of table, offsetting height by the same magnitude.
        transform.localPosition = new Vector3(transform.localPosition.x, yTableStart + (yHandleCurrent - yHandleStart), transform.localPosition.z);

        if (dynamicComponents.Count > 0)
        {
            bool simulate = false;
            foreach (IDynamic component in dynamicComponents)
            {
                if (component.UpdateState(numActiveCircuits))
                    simulate = true;
            }

            if (simulate)
                SimulateCircuit();
        }
    }

    public void ToggleLabels()
    {
        showLabels = !showLabels;
    }

    public void Reset()
    {
        GameObject[] dispensers;
        dispensers = GameObject.FindGameObjectsWithTag("Dispenser");
        foreach (GameObject dispenser in dispensers)
        {
            dispenser.GetComponent<IDispenser>().Reset();
        }

        GameObject[] pegs;
        pegs = GameObject.FindGameObjectsWithTag("Peg");
        foreach (GameObject peg in pegs)
        {
            peg.GetComponent<IPeg>().Reset();
        }

        board.Reset();
    }

    public void CreatePegs()
    {
        int i = 0;
        int j = 0;
        while (i++ < numRows)
        {
            while (j++ < numCols)
            {
                CreatePeg(i, j);
            }
        }
    }

    private void CreatePeg(int row, int col)
    {
        string name = "Peg_" + row.ToString() + "_" + col.ToString();

        var boardObject = GameObject.Find("Breadboard").gameObject;
        var mesh = boardObject.GetComponent<MeshFilter>().mesh;
        var size = mesh.bounds.size;
        var boardWidth = size.x * boardObject.transform.localScale.x;
        var boardHeight = size.z * boardObject.transform.localScale.z;

        var position = new Vector3(-(boardWidth / 2.0f) + ((col + 1) * pegInterval), pegHeight, -(boardHeight / 2.0f) + ((row + 1) * pegInterval));
        var rotation = Quaternion.Euler(new Vector3(0, 0, 0));
        var peg = Instantiate(pegTemplate, position, rotation) as GameObject;
        peg.transform.parent = boardObject.transform;
        peg.transform.localPosition = position;
        peg.transform.localRotation = rotation;
        peg.transform.localScale = pegScale;

        peg.name = name;

        Point coords = new Point(col, row);
        board.SetPegGameObject(coords, peg);
    }

    public void AddComponent(GameObject component, Point start, Point end)
    {
        string name = component.name;

        CircuitComponent cp = component.GetComponent<CircuitComponent>();
        if (cp == null)
        {
            Debug.Log($"ERROR: Component Type not found in {name}!");
            return;
        }

        PlacedComponent newComponent = new PlacedComponent(component, start, end);
        board.AddComponent(newComponent);

        BlockPegs(start, end, true);

        SimulateCircuit();
    }

    public void RemoveComponent(GameObject component, Point start)
    {
        Peg pegA = board.GetPeg(start);
        if (pegA != null)
        {
            PlacedComponent found = pegA.Components.Find(x => x.GameObject == component);
            if (found != null)
            {
                Peg pegB = board.GetPeg(found.End);
                if (pegB != null)
                {
                    if (!pegA.Components.Remove(found))
                        Debug.Log("Failed to remove component from Peg A!");
                    if (!pegB.Components.Remove(found))
                        Debug.Log("Failed to remove component from Peg B!");

                    board.Components.Remove(found);

                    BlockPegs(found.Start, found.End, false);
                }
            }
        }

        var script = component.GetComponent<CircuitComponent>();
        if (script != null)
        {
            script.SetActive(false, false);
        }

        SimulateCircuit();
    }

    public int GetFreeComponentSlots(Point start, int length)
    {
        int freeSlots = 0;

        if ((start.y + length < numRows) && (IsSlotFree(start, new Point(start.x, start.y + length), length)))
        {
            freeSlots++;
        }

        if ((start.y + length >= 0) && (IsSlotFree(start, new Point(start.x, start.y - length), length)))
        {
            freeSlots++;
        }

        if ((start.x + length < numCols) && (IsSlotFree(start, new Point(start.x + length, start.y), length)))
        {
            freeSlots++;
        }

        if ((start.x - length >= 0) && (IsSlotFree(start, new Point(start.x - length, start.y), length)))
        {
            freeSlots++;
        }

        return freeSlots;
    }

    protected bool LinesOverlap(Point startA, Point endA, Point startB, Point endB)
    {
        Point startC = startB;
        Point endC = endB;

        if (startA.x != startB.x || startA.y != startB.y)
        {
            startC = endB;
            endC = startB;
        }

        if ((endC.x > startC.x && endA.x > startA.x) ||
            (endC.x < startC.x && endA.x < startA.x) ||
            (endC.y > startC.y && endA.y > startA.y) ||
            (endC.y < startC.y && endA.y < startA.y))
        {
            return true;
        }

        return false;
    }

    public bool IsSlotFree(Point start, Point end, int length)
    {
        Peg pegStart = board.GetPeg(start);
        foreach (PlacedComponent component in pegStart.Components)
        {
            if (LinesOverlap(start, end, component.Start, component.End))
            {
                return false;
            }
        }

        List<Point> points = new List<Point>();
        if (start.x != end.x)
        {
            int xStart = (start.x < end.x ? start.x : end.x);
            int xEnd = (start.x < end.x ? end.x : start.x);
            for (int x = xStart; x <= xEnd; x++)
            {
                points.Add(new Point(x, start.y));
            }
        }
        if (start.y != end.y)
        {
            int yStart = (start.y < end.y ? start.y : end.y);
            int yEnd = (start.y < end.y ? end.y : start.y);
            for (int y = yStart; y <= yEnd; y++)
            {
                points.Add(new Point(start.x, y));
            }
        }

        if (length > 1)
        {
            for (int i = 1; i < length; i++)
            {
                Peg peg = board.GetPeg(points[i]);
                if (peg == null)
                {
                    return false;
                }

                if (peg.Components.Count > 0)
                {
                    return false;
                }
            }
        }

        foreach (Point pointA in points)
        {
            Peg pegA = board.GetPeg(pointA);
            if (pegA != null && pegA.IsBlocked)
            {
                return false;
            }

            foreach (Point pointB in points)
            {
                if (pointB.x == pointA.x && pointB.y == pointA.y)
                {
                    continue;
                }

                Peg pegB = board.GetPeg(pointB);
                if (pegB == null)
                {
                    return false;
                }

                foreach (PlacedComponent component in pegB.Components)
                {
                    if (((component.Start.x == pointA.x) && (component.Start.y == pointA.y)) ||
                        ((component.End.x == pointA.x) && (component.End.y == pointA.y)))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public void BlockPegs(Point start, Point end, bool block)
    {
        List<Point> points = new List<Point>();
        if (start.x != end.x)
        {
            int xStart = (start.x < end.x ? start.x : end.x);
            int xEnd = (start.x < end.x ? end.x : start.x);
            for (int x = xStart + 1; x < xEnd; x++)
            {
                Point coords = new Point(x, start.y);
                board.BlockPeg(coords, block);
            }
        }
        if (start.y != end.y)
        {
            int yStart = (start.y < end.y ? start.y : end.y);
            int yEnd = (start.y < end.y ? end.y : start.y);
            for (int y = yStart + 1; y < yEnd; y++)
            {
                Point coords = new Point(start.x, y);
                board.BlockPeg(coords, block);
            }
        }
    }

    public void RegisterDynamicComponent(IDynamic component)
    {
        dynamicComponents.Add(component);
    }

    public void UnregisterDynamicComponent(IDynamic component)
    {
        dynamicComponents.Remove(component);
    }

    public void SimulateCircuit()
    {
        numActiveCircuits = 0;

        int gen = ++board.Generation;

        List<PlacedComponent> batteries = new List<PlacedComponent>();
        foreach (PlacedComponent component in board.Components)
        {
            if (component.Component is IBattery)
            {
                batteries.Add(component);
            }
        }

        foreach (PlacedComponent battery in batteries)
        {
            if (battery.Generation == gen)
            {
                continue;
            }

            List<PlacedComponent> circuit = new List<PlacedComponent>();
            List<PlacedComponent> components = new List<PlacedComponent>();
            List<SpiceSharp.Entities.Entity> entities = new List<SpiceSharp.Entities.Entity>();

            circuit.Add(battery);
            components.Add(battery);

            int resistors = 0;

            Point currPosition = battery.End;
            Peg peg = board.GetPeg(currPosition);

            foreach (PlacedComponent component in peg.Components)
            {
                if (component != battery)
                {
                    FindCircuit(circuit, entities, components, component, currPosition, resistors, gen);
                }
            }

            if (battery.ShortCircuitGeneration == gen)
            {
                foreach (PlacedComponent component in components)
                {
                    if (component.ShortCircuitGeneration == gen)
                    {
                        component.Component.SetShortCircuit(true, component.ShortCircuitForward);
                    }
                    else
                    {
                        component.Component.SetActive(false, false);
                    }
                }

                if (!battery.ActiveShort)
                {
                    battery.ActiveShort = true;
                    battery.ActiveCircuit = false;
                    StartCoroutine(PlaySound(shortSound1, shortSound1StartTime));
                    StartCoroutine(PlaySound(shortSound2, shortSound2StartTime));
                }
            }
            else if (battery.Generation == gen)
            {
                numActiveCircuits++;
                var ssCircuit = new Circuit(entities);

                var op = new OP("DC 1");

                foreach (PlacedComponent component in components)
                {
                    bool isBattery = (component.Component is IBattery);

                    if (component.Generation == gen)
                    {
                        component.VoltageExport = new RealVoltageExport(op, isBattery ? component.End.ToString() : component.Start.ToString());
                        component.CurrentExport = new RealPropertyExport(op, "V" + component.GameObject.name, "i");
                    }
                }

                op.ExportSimulationData += (sender, args) =>
                {
                    var input = args.GetVoltage(battery.End.ToString());
                    var output = args.GetVoltage(battery.Start.ToString());

                    double minVoltage = 0f;
                    foreach (PlacedComponent component in components)
                    {
                        if (component.Generation == gen)
                        {
                            if (component.VoltageExport.Value < minVoltage)
                                minVoltage = component.VoltageExport.Value;
                        }
                    }

                    foreach (PlacedComponent component in components)
                    {
                        if (component.Generation == gen)
                        {
                            var voltage = component.VoltageExport.Value - minVoltage;
                            component.Component.SetVoltage(voltage);

                            var current = component.CurrentExport.Value;
                            component.Component.SetCurrent(current);
                        }
                    }
                };

                try
                {
                    op.Run(ssCircuit);

                    if (!battery.ActiveCircuit)
                    {
                        battery.ActiveCircuit = true;
                        battery.ActiveShort = false;
                        StartCoroutine(PlaySound(circuitSound, circuitSoundStartTime));
                    }
                }
                catch (ValidationFailedException exception)
                {
                    Debug.Log("Simulation Error! Caught exception: " + exception);
                    foreach (var rule in exception.Rules)
                    {
                        Debug.Log("  Rule: " + rule);
                        Debug.Log("  ViolationCount: " + rule.ViolationCount);
                        foreach (var violation in rule.Violations)
                        {
                            Debug.Log("    Violation: " + violation);
                            Debug.Log("    Subject: " + violation.Subject);
                        }
                    }
                    Debug.Log("Inner exception: " + exception.InnerException);

                    battery.ActiveShort = true;
                    battery.ActiveCircuit = false;
                    StartCoroutine(PlaySound(shortSound1, shortSound1StartTime));
                    StartCoroutine(PlaySound(shortSound2, shortSound2StartTime));

                    foreach (PlacedComponent component in components)
                    {
                        component.Component.SetActive(false, false);
                    }
                }
            }
            else
            {
                battery.ActiveShort = false;
                battery.ActiveCircuit = false;
            }
        }

        foreach (PlacedComponent component in board.Components)
        {
            if (component.Generation != gen)
            {
                component.Component.SetActive(false, false);
            }
        }

        foreach (PlacedComponent component in board.Components)
        {
            if (component.ShortCircuitGeneration != gen)
            {
                component.Component.SetShortCircuit(false, false);
            }
        }

    }

    private void FindCircuit(List<PlacedComponent> circuit, List<SpiceSharp.Entities.Entity> entities, List<PlacedComponent> components,
        PlacedComponent component, Point currPosition, int resistors, int gen)
    {
        var script = component.GameObject.GetComponent<CircuitComponent>();
        if (script == null || !script.IsClosed)
        {
            return;
        }

        circuit.Add(component);
        components.Add(component);

        if (component.Component is IResistor)
        {
            resistors++;
        }

        Point position = circuit[0].Start;
        Point nextPosition = component.End;
        if (nextPosition.y == currPosition.y && nextPosition.x == currPosition.x)
        {
            nextPosition = component.Start;
        }

        Peg peg = board.GetPeg(nextPosition);

        foreach (PlacedComponent nextComponent in peg.Components)
        {
            if (nextComponent == component)
            {
                continue;
            }

            if ((nextComponent == circuit[0]) &&
                (nextPosition.x == nextComponent.Start.x) &&
                (nextPosition.y == nextComponent.Start.y))
            {

                if (resistors == 0)
                {
                    foreach (PlacedComponent shortComponent in circuit)
                    {
                        bool forward = (position.y == shortComponent.Start.y) && (position.x == shortComponent.Start.x);

                        shortComponent.Generation = gen;
                        shortComponent.ShortCircuitGeneration = gen;
                        shortComponent.ShortCircuitForward = forward;

                        position = forward ? shortComponent.End : shortComponent.Start;
                    }
                    break;
                }

                foreach (PlacedComponent activeComponent in circuit)
                {
                    bool forward = (position.y == activeComponent.Start.y) && (position.x == activeComponent.Start.x);

                    if (activeComponent.Generation != gen)
                    {
                        AddSpiceSharpEntity(entities, activeComponent, forward);
                    }

                    activeComponent.Generation = gen;

                    script = activeComponent.GameObject.GetComponent<CircuitComponent>();
                    if (script != null)
                    {
                        script.SetActive(true, forward);
                    }

                    position = forward ? activeComponent.End : activeComponent.Start;
                }
            }
            else
            {
                foreach (PlacedComponent previousComponent in circuit)
                {
                    if (previousComponent == nextComponent)
                    {
                        circuit.Remove(component);
                        return;
                    }
                }

                FindCircuit(circuit, entities, components, nextComponent, nextPosition, resistors, gen);
            }
        }

        circuit.Remove(component);
    }

    void AddSpiceSharpEntity(List<SpiceSharp.Entities.Entity> entities, PlacedComponent placedComponent, bool forward)
    {
        string name = placedComponent.GameObject.name;
        string start = forward ? placedComponent.Start.ToString() : placedComponent.End.ToString();
        string end = forward ? placedComponent.End.ToString() : placedComponent.Start.ToString();
        string mid = name;
        CircuitComponent component = placedComponent.Component;

        if (component is IBattery battery)
        {
            if (entities.Count == 0)
            {
                entities.Add(new VoltageSource("V" + name, start, "0", 0f));
                entities.Add(new VoltageSource(name, "0", end, battery.BatteryVoltage));
            }
            else
            {
                entities.Add(new VoltageSource("V" + name, start, mid, 0f));
                entities.Add(new VoltageSource(name, mid, end, battery.BatteryVoltage));
            }
        }
        else if (component is IResistor resistor)
        {
            entities.Add(new VoltageSource("V" + name, mid, start, 0f));
            entities.Add(new Resistor(name, mid, end, resistor.Resistance));
        }
        else if (component is IConductor)
        {
            entities.Add(new VoltageSource("V" + name, mid, start, 0f));
            entities.Add(new LosslessTransmissionLine(name, mid, end, end, mid));
        }
        else
        {
            Debug.Log("Unrecognized component: " + name);
        }
    }

    IEnumerator PlaySound(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        StartCoroutine(FadeOut(source, fadeDelay, fadeTime));

        source.Stop();
        source.Play();
    }

    public static IEnumerator FadeOut(AudioSource source, float delay, float time)
    {
        yield return new WaitForSeconds(delay);

        float startVolume = source.volume;

        while (source.volume > 0)
        {
            source.volume -= startVolume * Time.deltaTime / time;

            yield return null;
        }

        source.Stop();
        source.volume = startVolume;
    }

}