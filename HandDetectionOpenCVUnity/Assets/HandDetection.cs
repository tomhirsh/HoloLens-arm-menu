using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnityExample;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using UnityEngine.XR.WSA.Input;
using HoloToolkit.Unity;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

[RequireComponent(typeof(WebCamTextureToMatHelper))]
public class HandDetection : MonoBehaviour

{


    /// <summary>
    /// The detector.
    /// </summary>
    ColorBlobDetector detector;

    /// <summary>
    /// The texture.
    /// </summary>
    //Texture2D texture;

    /// <summary>
    /// The spectrum size.
    /// </summary>
    Size SPECTRUM_SIZE;

    /// <summary>
    /// The contour color.
    /// </summary>
    Scalar CONTOUR_COLOR;

    /// <summary>
    /// The biggest contour color.
    /// </summary>
    Scalar BIGGEST_CONTOUR_COLOR;

    /// <summary>
    /// The contour color white.
    /// </summary>
    Scalar CONTOUR_COLOR_WHITE;

    float HAND_CONTOUR_AREA_THRESHOLD = 6000;

    /// <summary>
    /// The threashold slider.
    /// </summary>
    public Slider threasholdSlider;

    /// <summary>
    /// The BLOB color hsv.
    /// </summary>
    Scalar blobColorHsv;

    /// <summary>
    /// The spectrum mat.
    /// </summary>
    Mat spectrumMat;

    /// <summary>
    /// The webcam texture to mat helper.
    /// </summary>
    WebCamTextureToMatHelper webCamTextureToMatHelper;
    private Point armCenter = new Point(-1, -1);
    private double armAngle = 0;
    private Vector3 offset;
    private Vector3 camPosition;

    static float TIMER_INIT_VAL = 5.0f;
    double deltaFor90Degrees = 10;

    float distanceFromCam = 2.2f;
    bool isHandDetected = false;

    bool didHitPlane = false;

    Transform[] buttonsTransforms;

    GameObject camera;
    //GameObject cube;
    GameObject sphereColor;
    GameObject fireEffect;
    AudioSource audioSmoke;

    float timer = -5.0f;


    Vector3 CalculateScreenPosition(Vector3 p)
    {
        return (new Vector3((((float)p.x)) - 5, 5 - (((float)p.y)), 0)) * 5.0f;
    }

    double calculateDistance(Vector3 a, Vector3 b)
    {
        return (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y);
    }

    private void ShowPlane()
    {
        //Added without debugging
        //if (gameObject.GetComponent<Renderer>().enabled)
        //{
        //    return;
        //}
        ////
        ///

        if (timer > 0)
        {
            return;
        }

        gameObject.GetComponent<Renderer>().enabled = true;
        // enable all children (buttons) renderer
        Renderer[] renderChildren = gameObject.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderChildren.Length; ++i)
        {
            renderChildren[i].GetComponent<Renderer>().enabled = true;
        }
    }

    private void HidePlane()
    {
        //Added without debugging
        //if (!gameObject.GetComponent<Renderer>().enabled)
        //{
        //    return;
        //}
        //




        gameObject.GetComponent<Renderer>().enabled = false;
        // disable all children (buttons) renderer
        foreach (Renderer buttonRenderer in gameObject.GetComponentsInChildren<Renderer>())
        {
            buttonRenderer.enabled = false;
        }
    }


    #region InteractionManager

    private void InteractionManager_InteractionSourceDetected(InteractionSourceDetectedEventArgs args)
    {
        isHandDetected = true;
        //if (!gameObject.GetComponent<Renderer>().enabled)
        //    gameObject.GetComponent<Renderer>().enabled = true;

        ShowPlane();

        uint id = args.state.source.id;

        if (args.state.sourcePose.TryGetPosition(out Vector3 pos))
        {
            Vector3 cameraPosition = camera.GetComponent<Camera>().transform.position;
            gameObject.transform.position = distanceFromCam * (Vector3.Normalize(((pos - offset - cameraPosition) * 5.0f) - cameraPosition));
        }
    }

    private void InteractionManager_InteractionSourceUpdated(InteractionSourceUpdatedEventArgs args)
    {
        uint id = args.state.source.id;

        if (args.state.source.kind == InteractionSourceKind.Hand)
        {
            if (args.state.sourcePose.TryGetPosition(out Vector3 pos))
            {
                Vector3 cameraPosition = camera.GetComponent<Camera>().transform.position;
                gameObject.transform.position = distanceFromCam * (Vector3.Normalize(((pos - offset - cameraPosition) * 5.0f) - cameraPosition));
            }
        }
    }

    private void InteractionManager_InteractionSourceLost(InteractionSourceLostEventArgs args)
    {
        isHandDetected = false;
    }
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        camera = GameObject.FindWithTag("MainCamera");
        //cube = GameObject.FindWithTag("Player");
        sphereColor = GameObject.FindWithTag("GameController");
        fireEffect = GameObject.FindWithTag("Finish");
        audioSmoke = GameObject.FindWithTag("Sound").GetComponent<AudioSource>();
        buttonsTransforms = gameObject.transform.GetChild(0).GetComponentsInChildren<Transform>();

        InteractionManager.InteractionSourceDetected += InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated += InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost += InteractionManager_InteractionSourceLost;

        HidePlane();

        webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper>();

        webCamTextureToMatHelper.Initialize();

        detector = new ColorBlobDetector();
        spectrumMat = new Mat();
        blobColorHsv = new Scalar(255);
        SPECTRUM_SIZE = new Size(200, 64);
        CONTOUR_COLOR = new Scalar(255, 0, 0, 255);
        CONTOUR_COLOR_WHITE = new Scalar(255, 255, 255, 255);
        BIGGEST_CONTOUR_COLOR = new Scalar(0, 255, 0, 255);

        // set color in image
        Scalar hand_color = new Scalar(16, 92, 177, 0);
        detector.SetHsvColor(hand_color);
        Imgproc.resize(detector.GetSpectrum(), spectrumMat, SPECTRUM_SIZE);

    }

    /// <summary>
    /// Raises the web cam texture to mat helper initialized event.
    /// </summary>
    public void OnWebCamTextureToMatHelperInitialized()
    {

        Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();

        gameObject.transform.localScale = new Vector3(webCamTextureMat.cols(), webCamTextureMat.rows(), 1);

        float width = webCamTextureMat.width();
        float height = webCamTextureMat.height();

        float widthScale = (float)Screen.width / width;
        float heightScale = (float)Screen.height / height;
        if (widthScale < heightScale)
        {
            Camera.main.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
        }
        else
        {
            Camera.main.orthographicSize = height / 2;
        }
    }

    /// <summary>
    /// Raises the web cam texture to mat helper disposed event.
    /// </summary>
    public void OnWebCamTextureToMatHelperDisposed()
    {
        if (spectrumMat != null)
        {
            spectrumMat.Dispose();
            spectrumMat = null;
        }
    }

    public Vector3 CalculateNewPositionFromPicture(Point p)
    {
        //Getting the ray from the screen point where the finger is
        Ray res = Camera.main.ViewportPointToRay(new Vector3(((float)p.x / 640.0f), 1-((float)p.y / 640.0f), Camera.main.nearClipPlane));
        //Ray res = Camera.main.ViewportPointToRay(new Vector3(1 - ((float)p.x / 640.0f), 1 - ((float)p.y / 640.0f), Camera.main.nearClipPlane));
        var filter = GetComponent<MeshFilter>();

        //Getting a represention of the plane
        Vector3 normal;
        if (filter && filter.mesh.normals.Length > 0)
        {
            normal = filter.transform.TransformDirection(filter.mesh.normals[0]);
            var plane = new Plane(normal, transform.position);

            //Getting the intersaction between the plane and the finger (it is hitPoint)
            float enter = 0.0f;
            if (plane.Raycast(res, out enter))
            {
                Vector3 hitPoint = res.GetPoint(enter);
                didHitPlane = true;
                return hitPoint;
            }
            else
            {
                didHitPlane = false;
            }
        }
        return new Vector3(-1, -1, -1);
    }

    private double GetRadiusOfButton(int index)
    {
        return gameObject.transform.GetChild(index).GetComponent<SphereCollider>().radius;
    }

    private void HandPoseEstimationProcess(Mat rgbaMat)
    {
        // indication for making sphere coloring better
        Imgproc.GaussianBlur(rgbaMat, rgbaMat, new OpenCVForUnity.Size(3, 3), 1, 1);

        List<MatOfPoint> contours = detector.GetContours();

        detector.ProcessSkin(rgbaMat);
        detector.ProcessFinger(rgbaMat);

        if (contours.Count <= 0) //TODO: Add contour size
        {
            HidePlane();
            return;
        }

        if (!isHandDetected)
        {
            //Debug.Log("Contour size:" + detector.HandContourSize);
            if (detector.HandContourSize < HAND_CONTOUR_AREA_THRESHOLD)
            {
                HidePlane();
                return;
            }
            Moments moment = Imgproc.moments(detector.HandContours[0]);
            armCenter.x = moment.m10 / moment.m00;
            armCenter.y = moment.m01 / moment.m00;

            Ray res = Camera.main.ViewportPointToRay(new Vector3(((float)armCenter.x / 640.0f), ((float)armCenter.y / 640.0f), Camera.main.nearClipPlane));
            gameObject.transform.position = res.GetPoint(distanceFromCam);


            //Added without debugging!!!
            ShowPlane();
        }

        MatOfPoint2f elipseRes = new MatOfPoint2f(detector.HandContours[0].toArray());
        RotatedRect rotatedRect = Imgproc.fitEllipse(elipseRes);
        elipseRes.Dispose();
        armAngle = rotatedRect.angle;
        detector.ArmAngle = armAngle;
        double line_size = 0.14;

        //The gesture is not recognized at 90 degress!
        //if (armAngle >= 90 - deltaFor90Degrees && armAngle <= 90 + deltaFor90Degrees)
        //{
        //    gameObject.GetComponent<Renderer>().enabled = true;
        //    // enable all children (buttons) renderer
        //    Renderer[] renderChildren = gameObject.GetComponentsInChildren<Renderer>();
        //    for (int i = 0; i < renderChildren.Length; ++i)
        //    {
        //        renderChildren[i].GetComponent<Renderer>().enabled = true;
        //    }

        //    Moments moment1 = Imgproc.moments(detector.HandContours[0]);
        //    armCenter.x = moment1.m10 / moment1.m00;
        //    armCenter.y = moment1.m01 / moment1.m00;

        //    Vector3 offset = CalculateNewPositionFromPicture(armCenter);
        //    Vector3 newHandPosition = gameObject.transform.position + offset - previousOffset;
        //    newHandPosition.z = 4;
        //    gameObject.transform.position = newHandPosition;

        //    gameObject.GetComponent<Transform>().rotation = Quaternion.Euler(-25, 0, 0);

        //    return;
        //}
        //else if (armAngle == 0)
        //{
        //    gameObject.GetComponent<Renderer>().enabled = false;
        //    // disable all children (buttons) renderer
        //    Renderer[] renderChildren = gameObject.GetComponentsInChildren<Renderer>();
        //    for (int i = 0; i < renderChildren.Length; ++i)
        //    {
        //        renderChildren[i].GetComponent<Renderer>().enabled = false;
        //    }

        //}

        //Debug.Log("Arm angle: " + armAngle.ToString());

        if (armAngle > 90)
        {
            armAngle -= 180;
            offset = new Vector3((float)(-Math.Abs(line_size * Math.Sin((Math.PI / 180) * (armAngle)))),
                Math.Abs((float)(line_size * Math.Cos((Math.PI / 180) * (-armAngle)))), 0);
        }
        else {
            offset = new Vector3(Math.Abs((float)(line_size * Math.Sin((Math.PI / 180) * (-armAngle)))),
                Math.Abs((float)(line_size * Math.Cos((Math.PI / 180) * (-armAngle)))), 0);
        }

        Vector3 cameraRotation = (camera.GetComponent<Camera>().transform.rotation).eulerAngles;

        if (cameraRotation.y > 105 && cameraRotation.y < 260)
        {
            offset.x *= -1;
        }

        Point p = detector.NearestPoint;

        if (p.x == -1 || p.y == -1 || (detector.NearestPoint.x < 0) || !gameObject.GetComponent<Renderer>().enabled)
        {
            //cube.GetComponent<Renderer>().enabled = false;
            return;
        }

        // newPosition is the position of the finger
        Vector3 newPosition = CalculateNewPositionFromPicture(detector.NearestPoint);

        if (!didHitPlane)
        {
            return;
        }
        
        //cube.transform.position = newPosition;
        //cube.GetComponent<Renderer>().enabled = true;

        // first button
        Vector3 buttonPos1 = gameObject.transform.GetChild(0).position;
        newPosition.z = buttonPos1.z = 0;
        // second button
        Vector3 buttonPos2 = gameObject.transform.GetChild(1).position;
        // partical system - animation while pressing buttons

        double safeYDistance = 0.05; 
        double safeXDistance = 1.0;

        if (sphereColor != null)
        {
            if ((Math.Abs(newPosition.y - buttonPos1.y) <= safeYDistance) && (Math.Abs(newPosition.x - buttonPos1.x) <= safeXDistance))
            {
                // pressing button. do something
                PressButton(Color.yellow, 0);
            }
            else if ((Math.Abs(newPosition.y - buttonPos2.y) <= safeYDistance) && Math.Abs(newPosition.x - buttonPos2.x) <= safeXDistance)
            {
                // pressing button. do something
                PressButton(Color.red, 1);
            }
        }
    }

    private void PressButton(Color colorButton, int indexButton)
    {
        ParticleSystem fireSystem = fireEffect.GetComponent<ParticleSystem>();
        //var fireMainModule = fireSystem.main;
        sphereColor.GetComponent<Renderer>().material.color = colorButton;
        fireEffect.GetComponent<Transform>().position = gameObject.transform.GetChild(indexButton).position;
        fireSystem?.Play();
        audioSmoke.Play();
        timer = 1.0f;
        HidePlane();
    }

    private void LateUpdate()
    {
        if (armAngle >= 88 || armAngle<=-88)
        {
            armAngle = 90;
        } 
        //if (armAngle <= -88)
        //{
        //    armAngle = -90;
        //}
        transform.rotation = Quaternion.LookRotation(-Camera.main.transform.up, -Camera.main.transform.forward);
        //gameObject.transform.Rotate(-25.0f, 0, 0);
        Vector3 normal = Vector3.Normalize(camera.transform.position - gameObject.transform.position);
        //Debug.Log("Normal: " + normal.ToString());
        gameObject.transform.RotateAround(gameObject.transform.position, normal, (float)(armAngle));
    }


    // Update is called once per frame
    void Update()
    {
#if ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR)
            //Touch
            int touchCount = Input.touchCount;
            if (touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if(t.phase == TouchPhase.Ended && !EventSystem.current.IsPointerOverGameObject(t.fingerId)){
                    storedTouchPoint = new Point (t.position.x, t.position.y);
                    //Debug.Log ("touch X " + t.position.x);
                    //Debug.Log ("touch Y " + t.position.y);
                }
            }
#else
        //Mouse
#endif
        timer -= Time.deltaTime;

        if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
        {
            Mat rgbaMat = webCamTextureToMatHelper.GetMat();
            HandPoseEstimationProcess(rgbaMat);
        }
    }


    /// <summary>
    /// Raises the destroy event.
    /// </summary>
    void OnDestroy()
    {
        if (detector != null)
            detector.Dispose();
        if (spectrumMat != null)
        {
            spectrumMat.Dispose();
            spectrumMat = null;
        }
        InteractionManager.InteractionSourceDetected -= InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated -= InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost -= InteractionManager_InteractionSourceLost;
    }
}
