﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.VR;
using Kinect = Windows.Kinect;

public class AvatarController : MonoBehaviour
{
    public GameObject CameraContainer;
    public GameObject KinectManager;

    //We use a body prefab to generate the bodies of each tracked person
    public GameObject FullBodyPrefab;
    GameObject FullBody;

    public GameObject FrameworkPrefab;
    GameObject Framework;

    public GameObject LoadingBarPrefab;
    GameObject LoadingBar;

    public GameObject IntroScene;

    public bool AllowOvrvision = true;
    public bool Ovrvision { get; set; }
    public bool MonoTracking = true;

    [Range(1f, 100f)]
    public float smoothFactor = 5f;

    [Range(1f, 100f)]
    public float smoothFactorPositionSpineBase = 1f;

    [Range(1f, 100f)]
    public float smoothFactorRotationSpineBase = 1f;

    public enum ExercisesEnum { FullBody, LegLeft, LegRight, ArmLeft, ArmRight };
    public ExercisesEnum Exercises;

    public bool Mirrored { get; set; }

    public bool LegLeft { get; set; }
    public bool LegRight { get; set; }
    public bool ArmLeft { get; set; }
    public bool ArmRight { get; set; }

    //private AvatarScaler _AvatarScaler;
    private ChangeExercise _ChangeExercise;

    private float timeAmt = 5;
    private float time = 0;

    private bool firstRefreshBody = true;

    private int bodies = 0;

    private Dictionary<ulong, GameObject> _Bodies = new Dictionary<ulong, GameObject>();

    //This dictionary contains references of the bones of the instantiated body
    private Dictionary<ulong, Transform[]> _JointRig = new Dictionary<ulong, Transform[]>();

    private KinectManager _KinectManager;

    public Dictionary<int, Quaternion> _EulerOrientations = new Dictionary<int, Quaternion>()
    {
        {0, Quaternion.Euler(0, 0, 0)},
        {1, Quaternion.Euler(0, 0, 0)},
        {5, Quaternion.Euler(0, -90, -90)},
        {6, Quaternion.Euler(0, 0, -90)},
        {7, Quaternion.Euler(0, 0, -90)},
        {9, Quaternion.Euler(0, 90, 90)},
        {10, Quaternion.Euler(0, 0, 90)},
        {11, Quaternion.Euler(0, 0, 90)},
        {13, Quaternion.Euler(0, -90, 180)},
        {14, Quaternion.Euler(0, -90, 180)},
        {17, Quaternion.Euler(0, 90, 180)},
        {18, Quaternion.Euler(0, 90, 180)},
        {20, Quaternion.Euler(0, 0, 0)},
    };

    public Dictionary<int, Quaternion> _EulerOrientationsModeMirrored = new Dictionary<int, Quaternion>()
    {
        {0, Quaternion.Euler(0, 0, 0)},
        {1, Quaternion.Euler(0, 0, 0)},
        {5, Quaternion.Euler(0, 90, 90)},
        {6, Quaternion.Euler(0, 0, 90)},
        {7, Quaternion.Euler(0, 0, 90)},
        {9, Quaternion.Euler(0, -90, -90)},
        {10, Quaternion.Euler(0, 0, -90)},
        {11, Quaternion.Euler(0, 0, -90)},
        {13, Quaternion.Euler(0, 90, 180)},
        {14, Quaternion.Euler(0, 90, 180)},
        {17, Quaternion.Euler(0, -90, 180)},
        {18, Quaternion.Euler(0, -90, 180)},
        {20, Quaternion.Euler(0, 0, 0)},
    };

    public Dictionary<int, int> _JointReference = new Dictionary<int, int>()
    {
        {0, 0},
        {1, 0},
        {5, -1},
        {6, -1},
        {7, -1},
        {9, -1},
        {10, -1},
        {11, -1},
        {13, -1},
        {14, -1},
        {17, -1},
        {18, -1},
        {20, 0},
    };

    public Dictionary<int, int> _JointReferenceModeMirrored = new Dictionary<int, int>()
    {
        {0, 0},
        {1, 0},
        {5, +3},
        {6, +3},
        {7, +3},
        {9, -5},
        {10, -5},
        {11, -5},
        {13, +3},
        {14, +3},
        {17, -5},
        {18, -5},
        {20, 0},
    };

    //This dictionary will be used to find all the bones of the armature
    private Dictionary<string, int> _KinectToRig = new Dictionary<string, int>()
    {
        //Spine and head
        {"SpineBase", 0},
        {"SpineMid", 1},
        {"Neck", 2},//Unused
        {"Head", 3},
        {"SpineShoulder", 20},
        
        //Left arm
        {"ShoulderLeft", 4},
        {"ElbowLeft", 5},
        {"WristLeft", 6},
        {"HandLeft", 7},//Unused
        {"HandTipLeft", 21},//Unused
        {"ThumbLeft", 22},//Unused

        //Right arm
        {"ShoulderRight", 8},
        {"ElbowRight", 9},
        {"WristRight", 10},
        {"HandRight", 11},//Unused
        {"HandTipRight", 23},//Unused
        {"ThumbRight", 24},//Unused

        //Left leg
        {"HipLeft", 12},
        {"KneeLeft", 13},
        {"AnkleLeft", 14},
        {"FootLeft", 15},//Unused

        //Right leg
        {"HipRight", 16},
        {"KneeRight", 17},
        {"AnkleRight", 18},
        {"FootRight", 19},//Unused
    };

    void Start()
    {
        //_AvatarScaler = GetComponent<AvatarScaler>();
        _ChangeExercise = GetComponent<ChangeExercise>();

        LegLeft = true;
        LegRight = true;
        //ArmLeft = true;
        //ArmRight = true;
    }

    void Update()
    {
        CameraManager();

        //we need to store the keys of the tracked bodies in order to generate them
        //We check if the KinectManager has data to work with
        if (KinectManager == null)
        {
            return;
        }
        _KinectManager = KinectManager.GetComponent<KinectManager>();
        if (_KinectManager == null)
        {
            return;
        }
        //We store the data of the bodies detected
        Kinect.Body[] data = _KinectManager.GetData();
        if (data == null)
        {
            return;
        }

        //List of the tracked bodies (ulong is a an unsigned integer)
        List<ulong> trackedIds = new List<ulong>();

        foreach (var body in data)
        {
            if (body == null)
            {
                continue;
            }
            if (body.IsTracked)
            {
                trackedIds.Add(body.TrackingId);  //We add the ID of all the tracked body from the the current frame in the tracked body list
                if (!_Bodies.ContainsKey(body.TrackingId) && !(bodies > 0 && MonoTracking == true))
                {
                    //if the body isn't already in the _Bodies dictionnary, we create a new body object and add it to the dictionnary
                    _Bodies[body.TrackingId] = CreateBodyObject(body.TrackingId);

                    //We need to map the joints of the specific character to his body
                    _JointRig[body.TrackingId] = ReferenceRigToBody(body, _KinectToRig, _Bodies[body.TrackingId]);

                    bodies++;
                }
                if (_Bodies.ContainsKey(body.TrackingId))
                {
                    //otherwise the body exists already and we have to refresh it
                    RefreshBodyObject(body, _Bodies[body.TrackingId], _JointRig[body.TrackingId]);
                }
            }
        }

        List<ulong> knownIds = new List<ulong>(_Bodies.Keys);

        // First delete untracked bodies
        foreach (ulong trackingId in knownIds)
        {
            if (!trackedIds.Contains(trackingId))   //we check every Ids in knownIds and check if the body are still tracked. If it isn't the case we destroy the body
            {                                       //by updating the _Bodies dictionnary
                Destroy(_Bodies[trackingId]);
                _Bodies.Remove(trackingId);
                Destroy(Framework);
                Destroy(LoadingBar);
                bodies--;
            }
        }
    }

    /// <summary>
    /// Instantiate the prefab of the mesh and give it an id
    /// </summary>
    /// <returns>Transform list of all the joints in the order of the TypeJoints of the Kinect</returns>
    private GameObject CreateBodyObject(ulong id)
    {
        //We create a new body object named by the id
        FullBody = Instantiate(FullBodyPrefab) as GameObject;
        FullBody.name = FullBody.ToString() + id;

        Framework = Instantiate(FrameworkPrefab) as GameObject;
        Framework.name = Framework.ToString() + id;

        LoadingBar = Instantiate(LoadingBarPrefab) as GameObject;
        LoadingBar.name = LoadingBar.ToString() + id;
        LoadingBar.SetActive(false);

        UpdateWhenOffscreen(FullBody);

        return FullBody;
    }

    private void RefreshBodyObject(Kinect.Body body, GameObject bodyObject, Transform[] _JointRig)
    {
        //Scale the avatar is apparently useless. The camera follows the Head position and the proportions are the same than before.
        //_AvatarScaler.Scale(body, bodyObject, _JointRig);

        SetPositionToKinect(body, Kinect.JointType.SpineBase, _JointRig, firstRefreshBody);
        _ChangeExercise.ShowOrHideMember(bodyObject);
        if (firstRefreshBody)
        {
            CameraContainer.transform.position = new Vector3(_JointRig[3].position.x, _JointRig[3].position.y, _JointRig[3].position.z);
            Framework.transform.position = new Vector3(_JointRig[0].position.x - 0.0387f, _JointRig[3].position.y + 0.087f, 0);
            InputTracking.Recenter();
        }
        firstRefreshBody = false;

        Calibration(_JointRig);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            CameraContainer.transform.position = new Vector3(_JointRig[3].position.x, _JointRig[3].position.y, _JointRig[3].position.z);
            Framework.transform.position = new Vector3(_JointRig[0].position.x - 0.0387f, _JointRig[3].position.y + 0.087f, 0);
            InputTracking.Recenter();
        }

        _ChangeExercise.Change(body, _JointRig);
    }

    /// <summary>
    /// Map the Joints of the mesh to match the TypeJoints of the Kinect
    /// </summary>
    /// <returns>Transform list of all the joints in the order of the TypeJoints of the Kinect</returns>
    private Transform[] ReferenceRigToBody(Kinect.Body body, Dictionary<string, int> _KinectToRig, GameObject Fullbody)
    {
        //List<Transform> JointRig = new List<Transform>();

        Transform[] Rig = new Transform[30];

        //this loop will go through all the bones of the body from the parameter
        var list = Fullbody.transform.GetChild(0).gameObject.GetComponentsInChildren<Transform>();
        foreach (var child in list)
        {
            foreach (var element in _KinectToRig)
            {
                if (child.name.Equals(element.Key))
                {
                    Rig[element.Value] = child.transform;
                }
            }
        }
        return Rig;
    }

    /// <summary>
    /// Get the Quaternion rotation of the kinect joint
    /// </summary>
    /// <returns>Rotation of the tracked kinect joint in Quaternion</returns>
    public Quaternion JointOrientation(Kinect.Body body, Kinect.JointType jt)
    {
        Quaternion rotation = new Quaternion(body.JointOrientations[jt].Orientation.X, -body.JointOrientations[jt].Orientation.Y, -body.JointOrientations[jt].Orientation.Z, body.JointOrientations[jt].Orientation.W);
        return rotation;
    }

    /// <summary>
    /// Get the Quaternion rotation of the kinect joint Mirrored
    /// </summary>
    /// <returns>Rotation of the tracked kinect joint in Quaternion Mirrored</returns>
    public Quaternion JointOrientationMirrored(Kinect.Body body, Kinect.JointType jt)
    {
        Quaternion rotation = new Quaternion(body.JointOrientations[jt].Orientation.X, body.JointOrientations[jt].Orientation.Y, body.JointOrientations[jt].Orientation.Z, body.JointOrientations[jt].Orientation.W);
        return rotation;
    }

    /// <summary>
    /// Set the position of the mesh root to the position of the kinect joint 
    /// </summary>
    /// <returns>Void</returns>
    private void SetPositionToKinect(Kinect.Body body, Kinect.JointType jt, Transform[] _JointRig, bool firstRefreshBody)
    {
        Kinect.Joint SourceJoint = body.Joints[jt];
        Vector3 KinectPos = new Vector3(SourceJoint.Position.X * -1, SourceJoint.Position.Y, SourceJoint.Position.Z);
        if (firstRefreshBody)
        {
            _JointRig[0].position = KinectPos;
        }
        else
        {
            _JointRig[0].position = Vector3.Slerp(_JointRig[0].position, KinectPos, smoothFactorPositionSpineBase * Time.deltaTime);
        }
        return;
    }

    /// <summary>
    /// Smooth Factor 
    /// </summary>
    /// <returns>Void</returns>
    public void SetSmoothFactor(int RigIndex, Quaternion newRotation, Transform[] _JointRig, int jointReference)
    {
        //if (RigIndex == 0  || RigIndex == 13 || RigIndex == 17)
        if (RigIndex == 0)
        {
            _JointRig[RigIndex + jointReference].rotation = Quaternion.Slerp(_JointRig[RigIndex + jointReference].rotation, newRotation, smoothFactorRotationSpineBase * Time.deltaTime);
        }
        else
        {
            _JointRig[RigIndex + jointReference].rotation = Quaternion.Slerp(_JointRig[RigIndex + jointReference].rotation, newRotation, smoothFactor * Time.deltaTime);
        }
        return;
    }

    /// <summary>
    /// Check the box "Update When Offscreen" at the beginning
    /// </summary>
    /// <returns>Void</returns>
    private void UpdateWhenOffscreen(GameObject bodyObject)
    {
        Component[] skinnedMeshRenderers;
        skinnedMeshRenderers = bodyObject.GetComponentsInChildren(typeof(SkinnedMeshRenderer));
        if (skinnedMeshRenderers != null)
        {
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
                skinnedMeshRenderer.updateWhenOffscreen = true;
        }
        return;
    }

    /// <summary>
    /// Switch between OVRvision camera and normal camera and track calibration
    /// </summary>
    /// <returns>Void</returns>
    private void CameraManager()
    {
        if (AllowOvrvision)
        {
            if (Ovrvision)
            {
                CameraContainer.transform.GetChild(0).gameObject.SetActive(true);
                CameraContainer.transform.GetChild(1).gameObject.SetActive(false);
            }
            else
            {
                CameraContainer.transform.GetChild(0).gameObject.SetActive(false);
                CameraContainer.transform.GetChild(1).gameObject.SetActive(true);
            }
        }
        return;
    }

    /// <summary>
    /// Calibration with the red Framework and access the AR with the LoadingBar
    /// </summary>
    /// <returns>Void</returns>
    private void Calibration(Transform[] _JointRig)
    {
        //print("x : " + InputTracking.GetLocalRotation(VRNode.Head).x + " y : " + InputTracking.GetLocalRotation(VRNode.Head).y + " z : " + InputTracking.GetLocalRotation(VRNode.Head).z);
        if (Framework.activeSelf == true && InputTracking.GetLocalRotation(VRNode.Head).x < 0.01 && InputTracking.GetLocalRotation(VRNode.Head).x > -0.01 && InputTracking.GetLocalRotation(VRNode.Head).y < 0.01 && InputTracking.GetLocalRotation(VRNode.Head).y > -0.01 && InputTracking.GetLocalRotation(VRNode.Head).z < 0.01 && InputTracking.GetLocalRotation(VRNode.Head).z > -0.01)
        {
            Framework.SetActive(false);

            CameraContainer.transform.position = new Vector3(_JointRig[3].position.x, _JointRig[3].position.y, _JointRig[3].position.z);
            InputTracking.Recenter();

            LoadingBar.SetActive(true);
            LoadingBar.transform.position = new Vector3(_JointRig[0].position.x - 0.0387f, _JointRig[3].position.y + 0.087f, 0);
        }
        if (LoadingBar.activeSelf == true)
        {
            GameObject sliderGameObject;
            sliderGameObject = LoadingBar.transform.GetChild(0).GetChild(0).gameObject;
            Slider slider;
            slider = sliderGameObject.GetComponent<Slider>();

            if (InputTracking.GetLocalRotation(VRNode.Head).x < 0.05 && InputTracking.GetLocalRotation(VRNode.Head).x > -0.06 && InputTracking.GetLocalRotation(VRNode.Head).y < 0.04 && InputTracking.GetLocalRotation(VRNode.Head).y > -0.07)
            {
                if (time < timeAmt)
                {
                    time += Time.deltaTime;
                    slider.value = time / timeAmt;
                }
                else
                {
                    LoadingBar.SetActive(false);
                    if (AllowOvrvision)
                    {
                        if (SceneManager.GetActiveScene().name != "MainScene")
                        {
                            IntroScene.SetActive(false);
                        }
                        Ovrvision = true;
                    }
                }
            }
            else
            {
                time = 0;
                slider.value = 0;
            }
        }

        return;
    }
}
