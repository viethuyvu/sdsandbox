using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using PathCreation;


public class PathManager : MonoBehaviour
{
    public CarPath carPath;
    public PathCreator pathCreator;
    private GameObject currentEndTrigger;

    [Header("Path type")]
    public bool doMakeRandomPath = true;
    public bool doLoadScriptPath = false;
    public bool doLoadPointPath = false;
    public bool doLoadGameObjectPath = false;

    [Header("Path making")]
    public Transform startPos;
    public string pathToLoad = "none";
    public int smoothPathIter = 0;
    public GameObject locationMarkerPrefab;
    public int markerEveryN = 2;
    public bool doChangeLanes = false;
    public bool invertNodes = false;

    [Header("Random path parameters")]
    public int numSpans = 100;
    public float turnInc = 1f;
    public float spanDist = 5f;
    public bool sameRandomPath = true;
    public int randSeed = 2;

    [Header("End Detection")]
    public GameObject endTriggerPrefab;   // assign a simple cube with a trigger collider
    public float endTriggerRadius = 5f;   // size of the trigger (if using sphere)

    [Header("Debug")]
    public bool doShowNodePath = false;
    public bool doShowCenterNodePath = false;
    public GameObject pathelem;

    [Header("Aux")]
    public GameObject[] initAfterCarPathLoaded; // Scripts using the IWaitCarPath interface to init after loading the CarPath
    public GameObject[] challenges; // Challenges using the IWaitCarPath interface to init after loading the CarPath or on private API call

    Vector3 span = Vector3.zero;
    GameObject generated_mesh;

    void Awake()
    {
        if (sameRandomPath)
            Random.InitState(randSeed);

        InitCarPath();
    }

    public void InitCarPath()
    {
        // Clean up old debug nodes from previous generation
        GameObject[] oldNodes = GameObject.FindGameObjectsWithTag("pathNode");
        foreach (GameObject node in oldNodes)
        {
            Destroy(node);
        }

        if (doMakeRandomPath)
        {
            MakeRandomPath();
        }
        else if (doLoadScriptPath)
        {
            MakeScriptedPath();
        }
        else if (doLoadPointPath)
        {
            MakePointPath();
        }
        else if (doLoadGameObjectPath)
        {
            MakeGameObjectPath();
        }

        if (carPath == null) // if no carPath was created, skip the following block of code
        {
            return;
        }

        if (invertNodes)
        {
            CarPath new_carPath = new CarPath();
            for (int i = carPath.nodes.Count - 1; i > 0; i--)
            {
                PathNode node = carPath.nodes[i];
                node.rotation = node.rotation * Quaternion.AngleAxis(180, Vector3.up);
                new_carPath.nodes.Add(node);
                new_carPath.centerNodes.Add(node);
            }
            carPath = new_carPath;
        }

        if (startPos != null)
        {
            // Get the closest point to the start and make it index 0 of carPath
            int startIndex = 0;
            float closest = float.MaxValue;
            for (int i = 0; i < carPath.nodes.Count; i++)
            {
                PathNode node = carPath.nodes[i];
                float distance = Vector3.Distance(node.pos, startPos.position);
                if (distance < closest)
                {
                    closest = distance;
                    startIndex = i;
                }
            }

            if (startIndex != 0)
            {
                CarPath new_carPath = new CarPath();
                for (int i = startIndex; i < carPath.nodes.Count + startIndex; i++)
                {
                    if (i % carPath.nodes.Count == 0) { continue; } // avoid two consecutive values to be the same

                    PathNode node = carPath.nodes[i % carPath.nodes.Count];
                    new_carPath.nodes.Add(node);
                    new_carPath.centerNodes.Add(node);

                }
                // // close the loop
                // new_carPath.nodes.Add(new_carPath.nodes[0]);
                // new_carPath.centerNodes.Add(new_carPath.nodes[0]);

                carPath = new_carPath;
            }
        }

        // execute in the next update loop
        UnityMainThreadDispatcher.Instance().Enqueue(InitAfterCarPathLoaded(initAfterCarPathLoaded));
        UnityMainThreadDispatcher.Instance().Enqueue(InitAfterCarPathLoaded(challenges));

        // if (locationMarkerPrefab != null && carPath != null)
        // {
        //     int iLocId = 0;
        //     for (int iN = 0; iN < carPath.nodes.Count; iN += markerEveryN)
        //     {
        //         Vector3 np = carPath.nodes[iN].pos;
        //         GameObject go = Instantiate(locationMarkerPrefab, np, Quaternion.identity) as GameObject;
        //         go.transform.parent = this.transform;
        //         go.GetComponent<LocationMarker>().id = iLocId;
        //         iLocId++;
        //     }
        // }

        if (doShowNodePath)
        {
            for (int iN = 0; iN < carPath.nodes.Count; iN++)
            {
                Vector3 np = carPath.nodes[iN].pos;
                Quaternion rotation = carPath.nodes[iN].rotation;
                GameObject go = Instantiate(pathelem, np, rotation) as GameObject;
                go.tag = "pathNode";
                go.transform.parent = this.transform;
            }
        }

        if (doShowCenterNodePath)
        {
            for (int iN = 0; iN < carPath.centerNodes.Count; iN++)
            {
                Vector3 np = carPath.centerNodes[iN].pos;
                Quaternion rotation = carPath.centerNodes[iN].rotation;
                GameObject go = Instantiate(pathelem, np, rotation) as GameObject;
                go.tag = "pathNode";
                go.transform.parent = this.transform;
            }
        }
    }

    public IEnumerator InitAfterCarPathLoaded(GameObject[] scriptList)
    {
        if (carPath != null)
        {
            foreach (GameObject go in scriptList) // Init each Object that need a carPath
            {
                try
                {
                    IWaitCarPath script = go.GetComponent<IWaitCarPath>();
                    if (script != null)
                    {
                        script.Init();
                    }
                    else
                    {
                        Debug.LogError("Provided GameObject doesn't contain an IWaitCarPath script");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError(string.Format("Could not initialize: {0}, Exception: {1}", go.name, e));
                }
            }
        }

        else
        {
            Debug.LogError("No carPath loaded"); yield return null;
        }
        yield return null;
    }

    public Vector3 GetPathStart()
    {
        return startPos.position;
    }

    public Vector3 GetPathEnd()
    {
        int iN = carPath.nodes.Count - 1;

        if (iN < 0)
            return GetPathStart();

        return carPath.nodes[iN].pos;
    }

    float nfmod(float a, float b) // formula for negative and positive modulo
    {
        return a - b * Mathf.Floor(a / b);
    }

    void MakeGameObjectPath(float precision = 3f)
    {
        carPath = new CarPath();

        List<Vector3> points = new List<Vector3>();

        float stepping = 1 / (pathCreator.path.length * precision);
        for (float i = 0; i < 1; i += stepping)
        {
            points.Add(pathCreator.path.GetPointAtTime(i));
        }


        while (smoothPathIter > 0) // not working for the moment, looking forward using the same system as MakePointPath with LookAt
        {
            points = Chaikin(points);
            smoothPathIter--;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[(int)nfmod(i, (points.Count - 1))];
            Vector3 previous_point = points[(int)nfmod(i - 1, (points.Count - 1))];
            Vector3 next_point = points[(int)nfmod(i + 1, (points.Count - 1))];

            PathNode p = new PathNode();
            p.pos = point;
            p.rotation = Quaternion.LookRotation(next_point - previous_point, Vector3.up);
            carPath.nodes.Add(p);
            carPath.centerNodes.Add(p);
        }
    }

    void MakePointPath()
    {
        string filename = pathToLoad;

        TextAsset bindata = Resources.Load("Track/" + filename) as TextAsset;

        if (bindata == null)
            return;

        string[] lines = bindata.text.Split('\n');

        Debug.Log(string.Format("found {0} path points. to load", lines.Length));

        carPath = new CarPath();

        Vector3 np = Vector3.zero;

        float offsetY = -0.1f;
        List<Vector3> points = new List<Vector3>();

        foreach (string line in lines)
        {
            string[] tokens = line.Split(',');

            if (tokens.Length != 3)
                continue;
            np.x = float.Parse(tokens[0], CultureInfo.InvariantCulture.NumberFormat);
            np.y = float.Parse(tokens[1], CultureInfo.InvariantCulture.NumberFormat) + offsetY;
            np.z = float.Parse(tokens[2], CultureInfo.InvariantCulture.NumberFormat);

            points.Add(np);
        }

        while (smoothPathIter > 0)
        {
            points = Chaikin(points);
            smoothPathIter--;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[(int)nfmod(i, (points.Count))];
            Vector3 previous_point = points[(int)nfmod(i - 1, (points.Count))];
            Vector3 next_point = points[(int)nfmod(i + 1, (points.Count))];

            PathNode p = new PathNode();
            p.pos = point;
            p.rotation = Quaternion.LookRotation(next_point - previous_point, Vector3.up); ;
            carPath.nodes.Add(p);
            carPath.centerNodes.Add(p);
        }
    }

    public List<Vector3> Chaikin(List<Vector3> pts)
    {
        List<Vector3> newPts = new List<Vector3>();
        int ptsCount = pts.Count;

        for (int j = 0; j < ptsCount; j++)
        {
            int i = j;
            if (j < 0) { i = j + ptsCount; }
            newPts.Add(pts[i] + (pts[(i + 1)%ptsCount] - pts[i]) * 0.75f);
            newPts.Add(pts[(i + 1)%ptsCount] + (pts[(i + 2)%ptsCount] - pts[(i + 1)%ptsCount]) * 0.25f);
        }

        // newPts.Add(pts[pts.Count - 1]);
        return newPts;
    }


    void MakeScriptedPath()
    {
        TrackScript script = new TrackScript();

        if (script.Read(pathToLoad))
        {
            carPath = new CarPath();
            TrackParams tparams = new TrackParams();
            tparams.numToSet = 0;
            tparams.rotCur = Quaternion.identity;
            tparams.lastPos = startPos.position;

            float dY = 0.0f;
            float turn = 0f;

            Vector3 s = startPos.position;
            s.y = 0.5f;
            span.x = 0f;
            span.y = 0f;
            span.z = spanDist;
            float turnVal = 10.0f;

            List<Vector3> points = new List<Vector3>();

            foreach (TrackScriptElem se in script.track)
            {
                if (se.state == TrackParams.State.AngleDY)
                {
                    turnVal = se.value;
                }
                else if (se.state == TrackParams.State.CurveY)
                {
                    turn = 0.0f;
                    dY = se.value * turnVal;
                }
                else
                {
                    dY = 0.0f;
                    turn = 0.0f;
                }

                for (int i = 0; i < se.numToSet; i++)
                {

                    Vector3 np = s;
                    PathNode p = new PathNode();
                    p.pos = np;
                    points.Add(np);

                    turn = dY;

                    Quaternion rot = Quaternion.Euler(0.0f, turn, 0f);
                    span = rot * span.normalized;
                    span *= spanDist;
                    s = s + span;
                }

            }


            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[(int)nfmod(i, (points.Count))];
                Vector3 previous_point = points[(int)nfmod(i - 1, (points.Count))];
                Vector3 next_point = points[(int)nfmod(i + 1, (points.Count))];

                PathNode p = new PathNode();
                p.pos = point;
                p.rotation = Quaternion.LookRotation(next_point - previous_point, Vector3.up); ;
                carPath.nodes.Add(p);
                carPath.centerNodes.Add(p);
            }

        }
    }

    // Returns true if the two line segments (a1-a2 and b1-b2) intersect in 2D (XZ plane).
    // If includeEndpoints is false, intersections exactly at endpoints are ignored.
    private bool SegmentsIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, bool includeEndpoints = false)
    {
        float x1 = a1.x, z1 = a1.z;
        float x2 = a2.x, z2 = a2.z;
        float x3 = b1.x, z3 = b1.z;
        float x4 = b2.x, z4 = b2.z;

        float denom = (x1 - x2) * (z3 - z4) - (z1 - z2) * (x3 - x4);
        if (Mathf.Abs(denom) < 1e-6f) return false; // parallel

        float t = ((x1 - x3) * (z3 - z4) - (z1 - z3) * (x3 - x4)) / denom;
        float u = ((x1 - x3) * (z1 - z2) - (z1 - z3) * (x1 - x2)) / denom;

        if (includeEndpoints)
            return (t >= 0 && t <= 1 && u >= 0 && u <= 1);
        else
            return (t > 0 && t < 1 && u > 0 && u < 1);
    }

    private bool IsPathSelfIntersecting(List<Vector3> points)
    {
        int count = points.Count;
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 2; j < count - 1; j++) // j > i+1 to skip adjacent segments
            {
                if (SegmentsIntersect(points[i], points[i + 1], points[j], points[j + 1], false))
                    return true;
            }
        }
        return false;
    }

    void MakeRandomPath()
    {
        const int maxAttempts = 1000;
        int attempts = 0;
        bool validPathFound = false;
        List<Vector3> finalPoints = null;

        Vector3 start = startPos.position;
        start.y = 0.5f; // fixed height

        while (!validPathFound && attempts < maxAttempts)
        {
            attempts++;

            // ----- Generate random walk (original logic) -----
            Vector3 s = start;
            float turn = 0f;
            span.x = 0f;
            span.y = 0f;
            span.z = spanDist;

            List<Vector3> points = new List<Vector3>();
            points.Add(s);

            for (int iS = 0; iS < numSpans; iS++)
            {
                float t = Random.Range(-turnInc, turnInc);
                turn += t;

                Quaternion rot = Quaternion.Euler(0f, turn, 0f);
                span = rot * span.normalized;

                // Quick local avoidance (original heuristic)
                if (SegmentCrossesPath(s + (span.normalized * 100f), 90f, points.ToArray()))
                {
                    turn *= -0.5f;
                    rot = Quaternion.Euler(0f, turn, 0f);
                    span = rot * span.normalized;
                }

                span *= spanDist;
                s = s + span;
                points.Add(s);
            }

            // ----- Accurate self‑intersection check -----
            if (IsPathSelfIntersecting(points))
            {
                // Path crosses itself – reject and retry
                continue;
            }

            // If we get here, the path is valid
            finalPoints = points;
            validPathFound = true;
        }

        if (!validPathFound)
        {
            Debug.LogError("Could not generate a non‑intersecting path after " + maxAttempts + " attempts. Using last attempt (may be invalid).");
            // Fallback: generate a simple circle
            finalPoints = new List<Vector3>();
            int numCirclePoints = 20;
            for (int i = 0; i <= numCirclePoints; i++)
            {
                float angle = i * Mathf.PI * 2f / numCirclePoints;
                finalPoints.Add(new Vector3(Mathf.Sin(angle) * 20f, 0.5f, Mathf.Cos(angle) * 20f));
            }
        }

        // ----- Build CarPath nodes (unchanged) -----
        carPath = new CarPath();
        for (int i = 0; i < finalPoints.Count; i++)
        {
            Vector3 point = finalPoints[i];
            Vector3 prev = finalPoints[(i - 1 + finalPoints.Count) % finalPoints.Count];
            Vector3 next = finalPoints[(i + 1) % finalPoints.Count];

            PathNode p = new PathNode();
            p.pos = point;
            p.rotation = Quaternion.LookRotation(next - prev, Vector3.up);
            carPath.nodes.Add(p);
            carPath.centerNodes.Add(p);
        }

        // ---------- NEW: Spawn end trigger at the last node ----------
        // Destroy previous trigger if it exists
        if (currentEndTrigger != null)
            Destroy(currentEndTrigger);

        if (carPath.nodes.Count > 0)
        {
            Vector3 lastPos = carPath.nodes[carPath.nodes.Count - 1].pos;

            if (endTriggerPrefab != null)
            {
                currentEndTrigger = Instantiate(endTriggerPrefab, lastPos, Quaternion.identity, transform);
            }
            else
            {
                // Fallback: create a sphere trigger
                GameObject triggerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                triggerObj.transform.position = lastPos;
                triggerObj.transform.localScale = Vector3.one * endTriggerRadius;
                // Remove the visual mesh
                Destroy(triggerObj.GetComponent<MeshRenderer>());
                // Ensure it's a trigger
                Collider col = triggerObj.GetComponent<Collider>();
                col.isTrigger = true;
                triggerObj.transform.parent = transform;
                currentEndTrigger = triggerObj;
            }

            // Add the EndTrigger script and set the pathManager reference
            EndTrigger triggerScript = currentEndTrigger.AddComponent<EndTrigger>();
            triggerScript.pathManager = this;
        }
        // -------------------------------------------------------------
    }


    //void MakeRandomPath()
    //{
    //    carPath = new CarPath();

    //    Vector3 s = startPos.position;
    //    float turn = 0f;
    //    s.y = 0.5f;

    //    span.x = 0f;
    //    span.y = 0f;
    //    span.z = spanDist;

    //    List<Vector3> points = new List<Vector3>();

    //    for (int iS = 0; iS < numSpans; iS++)
    //    {
    //        Vector3 np = s;
    //        points.Add(np);

    //        float t = UnityEngine.Random.Range(-1.0f * turnInc, turnInc);

    //        turn += t;

    //        Quaternion rot = Quaternion.Euler(0.0f, turn, 0f);
    //        span = rot * span.normalized;

    //        if (SegmentCrossesPath(np + (span.normalized * 100.0f), 90.0f, points.ToArray()))
    //        {
    //            //turn in the opposite direction if we think we are going to run over the path
    //            turn *= -0.5f;
    //            rot = Quaternion.Euler(0.0f, turn, 0f);
    //            span = rot * span.normalized;
    //        }

    //        span *= spanDist;

    //        s = s + span;
    //    }

    //    for (int i = 0; i < points.Count; i++)
    //    {
    //        Vector3 point = points[(int)nfmod(i, (points.Count))];
    //        Vector3 previous_point = points[(int)nfmod(i - 1, (points.Count))];
    //        Vector3 next_point = points[(int)nfmod(i + 1, (points.Count))];

    //        PathNode p = new PathNode();
    //        p.pos = point;
    //        p.rotation = Quaternion.LookRotation(next_point - previous_point, Vector3.up); ;
    //        carPath.nodes.Add(p);
    //        carPath.centerNodes.Add(p);
    //    }

    //}

    public bool SegmentCrossesPath(Vector3 posA, float rad, Vector3[] posN)
    {
        foreach (Vector3 pn in posN)
        {
            float d = (posA - pn).magnitude;

            if (d < rad)
                return true;
        }

        return false;
    }

    public void SetPath(CarPath p)
    {
        carPath = p;

        GameObject[] prev = GameObject.FindGameObjectsWithTag("pathNode");

        Debug.Log(string.Format("Cleaning up {0} old nodes. {1} new ones.", prev.Length, p.nodes.Count));

        foreach (PathNode pn in carPath.nodes)
        {
            GameObject go = Instantiate(pathelem, pn.pos, Quaternion.identity) as GameObject;
            go.tag = "pathNode";
        }
    }
}
