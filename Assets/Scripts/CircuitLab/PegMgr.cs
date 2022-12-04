using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PegMgr : MonoBehaviour, IPeg
{
    public AudioSource clickSound;
    public float clickStartTime = 0f;
    public float pegInterval = 0.1f;

    float shortPositionOffset = -2.5f;
    float longPositionOffset = -5.0f;

    float componentHeight = 0.5f;

    bool isOccupied = false;
    GameObject clone = null;
    GameObject original = null;
    CircuitComponent originalScript = null;

    void Start()
    {
        clickSound.GetComponent<AudioSource>();
    }

    IEnumerator PlaySound(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);

        source.Stop();
        source.Play();
    }

    void Update()
    {
        if (original && !originalScript.IsHeld)
        {
            if (originalScript.IsPlaced)
            {
                DestroyClone();
                original = null;
                return;
            }

            Point start = GetCoordinates();
            original.transform.parent = transform;
            original.gameObject.GetComponent<Rigidbody>().useGravity = false;
            original.gameObject.GetComponent<Rigidbody>().isKinematic = true;

            original.gameObject.GetComponent<BoxCollider>().enabled = false;
            original.gameObject.GetComponent<SphereCollider>().enabled = false;

            Point end = LockRotation(original, original);

            Direction direction = GetDirection(start, end);
            originalScript.Place(start, direction);

            DestroyClone();

            StartCoroutine(PlaySound(clickSound, clickStartTime));

            var lab = GameObject.Find("CircuitLab").gameObject;
            var script = lab.GetComponent<ICircuitLab>();
            if (script != null)
            {
                script.AddComponent(original, start, end);
            }

            original = null;
        }

        if (clone)
        {
            LockRotation(clone, original);
        }
    }

    public void Reset()
    {
        isOccupied = false;
        clone = null;
        original = null;
        originalScript = null;
    }

    Point GetCoordinates()
    {
        string name = transform.name.Substring(4);

        int row = int.Parse(name.Substring(0, name.IndexOf('_')));
        int col = int.Parse(name.Substring(name.IndexOf('_') + 1));

        return new Point(col, row);
    }

    Point LockRotation(GameObject target, GameObject reference)
    {
        float offset = shortPositionOffset;
        int pegOffset = 1;
        if (reference.transform.name.Contains("LongWire"))
        {
            offset = longPositionOffset;
            pegOffset = 2;
        }

        Point coords = GetCoordinates();
        Point end = new Point(coords.x, coords.y);

        string north = "Peg_" + (coords.y + pegOffset) + "_" + coords.x;
        Point ptNorth = new Point(coords.x, coords.y + pegOffset);
        string south = "Peg_" + (coords.y - pegOffset) + "_" + coords.x;
        Point ptSouth = new Point(coords.x, coords.y - pegOffset);
        string east = "Peg_" + coords.y + "_" + (coords.x + pegOffset);
        Point ptEast = new Point(coords.x + pegOffset, coords.y);
        string west = "Peg_" + coords.y + "_" + (coords.x - pegOffset);
        Point ptWest = new Point(coords.x - pegOffset, coords.y);

        var lab = GameObject.Find("CircuitLab").gameObject;
        var script = lab.GetComponent<ICircuitLab>();
        Point start = GetCoordinates();
        List<string> freeNeighbors = new List<string>();
        Point[] neighbors = { ptNorth, ptSouth, ptEast, ptWest };
        string[] neighborNames = { north, south, east, west };
        for (int i = 0; i < 4; i++)
        {
            if (script.IsSlotFree(start, neighbors[i], pegOffset))
            {
                freeNeighbors.Add(neighborNames[i]);
            }
        }

        string closest = GetClosestNeighbor(reference, freeNeighbors);

        var rotation = target.transform.localEulerAngles;
        var position = target.transform.localPosition;
        rotation.x = -90;
        rotation.y = 0;
        position.y = componentHeight;
        if (closest == north)
        {
            rotation.z = 0;
            position.x = 0;
            position.z = -offset;
            end.y += pegOffset;
        }
        else if (closest == east)
        {
            rotation.z = 90;
            position.x = -offset;
            position.z = 0;
            end.x += pegOffset;
        }
        else if (closest == south)
        {
            rotation.z = 180;
            position.x = 0;
            position.z = offset;
            end.y -= pegOffset;
        }
        else
        {
            rotation.z = 270;
            position.x = offset;
            position.z = 0;
            end.x -= pegOffset;
        }
        target.transform.localEulerAngles = rotation;
        target.transform.localPosition = position;

        return end;
    }

    void HighlightNeighbor(string name, Color color)
    {
        var neighbor = GameObject.Find(name);
        if (neighbor)
        {
            DrawLine(transform.position, neighbor.transform.position, color);
        }
    }

    Direction GetDirection(Point start, Point end)
    {
        if (end.y > start.y)
            return Direction.North;
        else if (end.y < start.y)
            return Direction.South;
        else if (end.x > start.x)
            return Direction.East;
        else
            return Direction.West;
    }

    string GetClosestNeighbor(GameObject clone, List<string> names)
    {
        string closest = names[0];
        GameObject closestNeighbor = null;
        float min = 999;
        var endpoint = clone.transform.Find("WireEnd2");

        foreach (string name in names)
        {
            GameObject neighbor = GameObject.Find(name);
            if (neighbor)
            {
                float nextDistance = Vector3.Distance(neighbor.transform.position, endpoint.transform.position);
                if (nextDistance < min)
                {
                    min = nextDistance;
                    closest = name;
                    closestNeighbor = neighbor;
                }
            }
        }

        return closest;
    }

    void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        GameObject myLine = new GameObject();
        myLine.transform.position = start;
        myLine.AddComponent<LineRenderer>();
        LineRenderer lr = myLine.GetComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        GameObject.Destroy(myLine, Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetType() != typeof(SphereCollider))
        {
            return;
        }

        if (!isOccupied && other.name.StartsWith("Component"))
        {
            int componentLength = 1;
            if (other.transform.name.Contains("LongWire"))
            {
                componentLength = 2;
            }

            var lab = GameObject.Find("CircuitLab").gameObject;
            var script = lab.GetComponent<ICircuitLab>();
            if (script == null)
            {
                return;
            }
            Point start = GetCoordinates();
            int freeSlots = script.GetFreeComponentSlots(start, componentLength);

            if (freeSlots == 0)
            {
                return;
            }

            original = other.gameObject;
            originalScript = original.GetComponent<CircuitComponent>();

            clone = Instantiate(other.gameObject);
            clone.GetComponent<Rigidbody>().detectCollisions = false;
            var cloneScript = clone.GetComponent<CircuitComponent>();
            cloneScript.IsClone = true;

            clone.gameObject.GetComponent<Rigidbody>().useGravity = false;
            clone.gameObject.GetComponent<Rigidbody>().isKinematic = false;

            clone.transform.parent = transform;
            isOccupied = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetType() != typeof(SphereCollider))
        {
            return;
        }

        if (other.name.StartsWith("Component"))
        {
            if (original)
            {
                original.transform.parent = null;
                original = null;
            }

            DestroyClone();
        }
    }

    void DestroyClone()
    {
        if (isOccupied)
        {
            Destroy(clone);
            clone = null;
            isOccupied = false;
        }
    }
}
