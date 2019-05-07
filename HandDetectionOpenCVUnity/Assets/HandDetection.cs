using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnityExample;
using UnityEngine.UI;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif


public class HandDetection : MonoBehaviour

{
    /// <summary>
    /// The detector.
    /// </summary>
    ColorBlobDetector detector;


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

    // Start is called before the first frame update
    void Start()
    {
        Utils.setDebugMode(true);

        Texture2D imgTexture = Resources.Load("arm") as Texture2D;

        double x = 235;
        double y = 329;

        Mat imgMat = new Mat(imgTexture.height, imgTexture.width, CvType.CV_8UC4);

        Utils.texture2DToMat(imgTexture, imgMat);
        Debug.Log("imgMat.ToString() " + imgMat.ToString());

        detector = new ColorBlobDetector();
        spectrumMat = new Mat();
        //blobColorRgba = new Scalar (255);
        blobColorHsv = new Scalar(255);
        SPECTRUM_SIZE = new Size(200, 64);
        //SPECTRUM_SIZE = new Size(256, 256);
        CONTOUR_COLOR = new Scalar(255, 0, 0, 255);
        CONTOUR_COLOR_WHITE = new Scalar(255, 255, 255, 255);
        BIGGEST_CONTOUR_COLOR = new Scalar(0, 255, 0, 255);
        //SetColorInImage(imgMat, new Point(228, 289));
        SetColorInImage(imgMat, new Point(x, y));

        HandPoseEstimationProcess(imgMat);

        Texture2D texture = new Texture2D(imgMat.cols(), imgMat.rows(), TextureFormat.RGBA32, false);

        Utils.matToTexture2D(imgMat, texture);

        gameObject.GetComponent<Renderer>().material.mainTexture = texture;


        Utils.setDebugMode(false);

    }

    private void HandPoseEstimationProcess(Mat rgbaMat)
    {
        //Imgproc.blur(mRgba, mRgba, new Size(5,5));
        Imgproc.GaussianBlur(rgbaMat, rgbaMat, new OpenCVForUnity.Size(3, 3), 1, 1);
        //Imgproc.medianBlur(mRgba, mRgba, 3);

        List<MatOfPoint> contours = detector.GetContours();

        detector.Process(rgbaMat);

        //                      Debug.Log ("Contours count: " + contours.Count);

        if (contours.Count <= 0)
        {
            return;
        }

        RotatedRect rect = Imgproc.minAreaRect(new MatOfPoint2f(contours[0].toArray()));

        double boundWidth = rect.size.width;
        double boundHeight = rect.size.height;
        int boundPos = 0;

        for (int i = 1; i < contours.Count; i++)
        {
            rect = Imgproc.minAreaRect(new MatOfPoint2f(contours[i].toArray()));
            if (rect.size.width * rect.size.height > boundWidth * boundHeight)
            {
                boundWidth = rect.size.width;
                boundHeight = rect.size.height;
                boundPos = i;
            }
        }

        MatOfPoint contour = contours[boundPos];

        OpenCVForUnity.Rect boundRect = Imgproc.boundingRect(new MatOfPoint(contour.toArray()));
        Imgproc.rectangle(rgbaMat, boundRect.tl(), boundRect.br(), CONTOUR_COLOR_WHITE, 2, 8, 0);

        //                      Debug.Log (
        //                      " Row start [" + 
        //                              (int)boundRect.tl ().y + "] row end [" +
        //                              (int)boundRect.br ().y + "] Col start [" +
        //                              (int)boundRect.tl ().x + "] Col end [" +
        //                              (int)boundRect.br ().x + "]");


        double a = boundRect.br().y - boundRect.tl().y;
        a = a * 0.7;
        a = boundRect.tl().y + a;

        //                      Debug.Log (
        //                      " A [" + a + "] br y - tl y = [" + (boundRect.br ().y - boundRect.tl ().y) + "]");

        Imgproc.rectangle(rgbaMat, boundRect.tl(), new Point(boundRect.br().x, a), CONTOUR_COLOR, 2, 8, 0);

        MatOfPoint2f pointMat = new MatOfPoint2f();
        Imgproc.approxPolyDP(new MatOfPoint2f(contour.toArray()), pointMat, 3, true);
        contour = new MatOfPoint(pointMat.toArray());

        MatOfInt hull = new MatOfInt();
        MatOfInt4 convexDefect = new MatOfInt4();
        Imgproc.convexHull(new MatOfPoint(contour.toArray()), hull);

        if (hull.toArray().Length < 3)
            return;

        Imgproc.convexityDefects(new MatOfPoint(contour.toArray()), hull, convexDefect);

        List<MatOfPoint> hullPoints = new List<MatOfPoint>();
        List<Point> listPo = new List<Point>();
        for (int j = 0; j < hull.toList().Count; j++)
        {
            listPo.Add(contour.toList()[hull.toList()[j]]);
        }

        MatOfPoint e = new MatOfPoint();
        e.fromList(listPo);
        hullPoints.Add(e);

        List<Point> listPoDefect = new List<Point>();

        if (convexDefect.rows() > 0)
        {
            List<int> convexDefectList = convexDefect.toList();
            List<Point> contourList = contour.toList();
            for (int j = 0; j < convexDefectList.Count; j = j + 4)
            {
                Point farPoint = contourList[convexDefectList[j + 2]];
                int depth = convexDefectList[j + 3];
                //if (depth > threasholdSlider.value && farPoint.y < a)
                if (farPoint.y < a)
                {
                    listPoDefect.Add(contourList[convexDefectList[j + 2]]);
                }
                //                              Debug.Log ("convexDefectList [" + j + "] " + convexDefectList [j + 3]);
            }
        }


        //                      Debug.Log ("hull: " + hull.toList ());
        //                      if (convexDefect.rows () > 0) {
        //                          Debug.Log ("defects: " + convexDefect.toList ());
        //                      }

        Imgproc.drawContours(rgbaMat, detector.HandContours , 0, BIGGEST_CONTOUR_COLOR, 3);
        Imgproc.drawContours(rgbaMat, hullPoints, -1, CONTOUR_COLOR, 3);

        //                      int defectsTotal = (int)convexDefect.total();
        //                      Debug.Log ("Defect total " + defectsTotal);

        foreach (Point p in listPoDefect)
        {
            Imgproc.circle(rgbaMat, p, 6, new Scalar(255, 0, 255, 255), -1);
        }
    }

    private void SetColorInImage(Mat img, Point touchPoint)
    {
        int cols = img.cols();
        int rows = img.rows();

        int x = (int)touchPoint.x;
        int y = (int)touchPoint.y;

        //Debug.Log ("Touch image coordinates: (" + x + ", " + y + ")");

        if ((x < 0) || (y < 0) || (x > cols) || (y > rows))
            return;

        OpenCVForUnity.Rect touchedRect = new OpenCVForUnity.Rect();

        touchedRect.x = (x > 5) ? x - 5 : 0;
        touchedRect.y = (y > 5) ? y - 5 : 0;

        touchedRect.width = (x + 5 < cols) ? x + 5 - touchedRect.x : cols - touchedRect.x;
        touchedRect.height = (y + 5 < rows) ? y + 5 - touchedRect.y : rows - touchedRect.y;

        using (Mat touchedRegionRgba = img.submat(touchedRect))
        using (Mat touchedRegionHsv = new Mat())
        {
            Imgproc.cvtColor(touchedRegionRgba, touchedRegionHsv, Imgproc.COLOR_RGB2HSV_FULL);

            // Calculate average color of touched region
            blobColorHsv = Core.sumElems(touchedRegionHsv);
            int pointCount = touchedRect.width * touchedRect.height;
            for (int i = 0; i < blobColorHsv.val.Length; i++)
                blobColorHsv.val[i] /= pointCount;

            //blobColorRgba = ConverScalarHsv2Rgba (blobColorHsv);            
            //Debug.Log ("Touched rgba color: (" + mBlobColorRgba.val [0] + ", " + mBlobColorRgba.val [1] +
            //  ", " + mBlobColorRgba.val [2] + ", " + mBlobColorRgba.val [3] + ")");

            detector.SetHsvColor(blobColorHsv);

            Imgproc.resize(detector.GetSpectrum(), spectrumMat, SPECTRUM_SIZE);

        }
    }

    // Update is called once per frame
    void Update()
    {
        
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
    }
}
