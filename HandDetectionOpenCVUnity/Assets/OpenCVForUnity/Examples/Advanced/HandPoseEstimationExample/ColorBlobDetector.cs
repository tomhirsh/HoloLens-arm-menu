using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using OpenCVForUnity;
using UnityEngine.XR.WSA.Input;

namespace OpenCVForUnityExample
{
    public class ColorBlobDetector
    {
        private class PointComparer : Comparer<Point>
        {
            public override int Compare(Point x, Point y)
            {
                if (x.y < y.y)
                {
                    return -1;
                }
                if (x.y > y.y)
                {
                    return 1;
                }
                return 0;
            }
        }
        // Lower and Upper bounds for range checking in HSV color space
        private Scalar mLowerBound = new Scalar(0);
        private Scalar mUpperBound = new Scalar(0);
        private Scalar mLowerBoundYCrCb = new Scalar(0);
        private Scalar mUpperBoundYCrCb = new Scalar(0);
        private Scalar mLowerBoundRGB = new Scalar(0);
        private Scalar mUpperBoundRGB = new Scalar(0);
        private Scalar mLowerBoundHSV = new Scalar(0);
        private Scalar mUpperBoundHSV = new Scalar(0);
        // Lower and Upper bounds for finger (purple)
        private Scalar fLowerBoundHSV2 = new Scalar(0);
        private Scalar fUpperBoundHSV2 = new Scalar(0);
        private Scalar fLowerBoundHSV = new Scalar(0);
        private Scalar fUpperBoundHSV = new Scalar(0);
        // Minimum contour area in percent for contours filtering
        private static double mMinContourArea = 0.1;
        // Color radius for range checking in HSV color space
        private Scalar mColorRadius = new Scalar(25, 50, 50, 0);
        private Mat mSpectrum = new Mat();
        private List<MatOfPoint> mContours = new List<MatOfPoint>();

        // Cache
        private Mat mPyrDownMat = new Mat();
        private Mat mHsvMat = new Mat();
        private Mat mRGBAMat = new Mat();
        private Mat mYCrCbMat = new Mat();
        private Mat mMask = new Mat();
        private Mat mMaskHSV = new Mat();
        private Mat mMaskRGB = new Mat();
        private Mat mMaskYCrCb = new Mat();
        // masks for puple finger
        private Mat fMask = new Mat();
        private Mat fMaskHSV = new Mat();
        private Mat fMaskHSV2 = new Mat();
        private Mat mDilatedMask = new Mat();
        private Mat mHierarchy = new Mat();

        private MatOfPoint handContour;
        private Mat fDilatedMask = new Mat();
        private Mat fHierarchy = new Mat();
        private PointComparer comparer = new PointComparer();

        public double ArmAngle { get; set; }

        public void SetColorRadius(Scalar radius)
        {
            mColorRadius = radius;
        }

        public Point NearestPoint
        {
            get
            {
                Point res = new Point(-1, -1);
                double minDistance = -1;
                double deltaFor90Degrees = 10;
                if (FingerContour == null)
                {
                    return res;
                }
                if (ArmAngle >= 90 - deltaFor90Degrees && ArmAngle <= 90 + deltaFor90Degrees)
                {
                    List<Point> pointList = FingerContour.toList();
                    pointList.Sort(comparer);
                    Point highestPoint = pointList[0];
                    return highestPoint;
                }
                foreach (Point handP in handContour.toArray())
                {
                    foreach (Point fingerP in FingerContour.toArray())
                    {
                        double currentDistance = (handP.x - fingerP.x) * (handP.x - fingerP.x) + (handP.y - fingerP.y) * (handP.y - fingerP.y);
                        if (currentDistance < minDistance || minDistance == -1)
                        {
                            res = handP;
                            minDistance = currentDistance;
                        }
                    }
                }
                return res;
            }
        }

        public void SetHsvColor(Scalar hsvColor)
        {
            double range = 1.4;
            double minH = (hsvColor.val[0] >= mColorRadius.val[0]) ? (hsvColor.val[0] - mColorRadius.val[0]) / range : 0;
            //double minH = hsvColor.val[0];//mColorRadius.val[0] - hsvColor.val[0];
            double maxH = (hsvColor.val[0] + mColorRadius.val[0] <= 255) ? (hsvColor.val[0] + mColorRadius.val[0]) * range : 255;

            mLowerBound.val[0] = minH;
            mUpperBound.val[0] = maxH;

            mLowerBound.val[1] = (hsvColor.val[1] - mColorRadius.val[1]) / range;
            mUpperBound.val[1] = (hsvColor.val[1] + mColorRadius.val[1]) * range;

            mLowerBound.val[2] = (hsvColor.val[2] - mColorRadius.val[2]) / range;
            mUpperBound.val[2] = (hsvColor.val[2] + mColorRadius.val[2]) * range;

            mLowerBound.val[3] = 0;
            mUpperBound.val[3] = 255;

            using (Mat spectrumHsv = new Mat(1, (int)(maxH - minH), CvType.CV_8UC3))
            {
                for (int j = 0; j < maxH - minH; j++)
                {
                    byte[] tmp = { (byte)(minH + j), (byte)255, (byte)255 };
                    spectrumHsv.put(0, j, tmp);
                }

                Imgproc.cvtColor(spectrumHsv, mSpectrum, Imgproc.COLOR_HSV2RGB_FULL, 4);
            }
            // skin
            //mLowerBoundHSV.val = new double[4] { 0, 40, 0, 0 };
            //mUpperBoundHSV.val = new double[4] { 25, 255, 255, 255 }; // Ortal colors

            //mLowerBoundYCrCb.val = new double[4] { 0, 138, 67, 0 }; // Ortal colors
            //mUpperBoundYCrCb.val = new double[4] { 255, 173, 133, 255 };

            //Two rows of lights!
            //mLowerBoundHSV.val = new double[4] { 0, 40, 0, 0 };
            //mUpperBoundHSV.val = new double[4] { 25, 255, 255, 255 };

            //mLowerBoundYCrCb.val = new double[4] { 0, 138, 67, 0 };
            //mUpperBoundYCrCb.val = new double[4] { 255, 173, 133, 255 };

            //Three rows of lights
            mLowerBoundHSV.val = new double[4] { 0, 50, 0, 0 };
            mUpperBoundHSV.val = new double[4] { 35, 255, 255, 255 };

            mLowerBoundYCrCb.val = new double[4] { 0, 130, 67, 0 };
            mUpperBoundYCrCb.val = new double[4] { 255, 173, 133, 255 };

            mLowerBoundRGB.val = new double[4] { 0, 0, 0, 0 };
            mUpperBoundRGB.val = new double[4] { 255, 255, 150, 255 };

            // finger (green)
            fLowerBoundHSV.val = new double[4] { 60, 50, 50, 0 };
            fUpperBoundHSV.val = new double[4] { 116, 255, 255, 255 };

        }

        public Mat GetSpectrum()
        {
            return mSpectrum;
        }

        public void SetMinContourArea(double area)
        {
            mMinContourArea = area;
        }

        public void ProcessFinger(Mat rgbaImage)
        {
            Imgproc.pyrDown(rgbaImage, mPyrDownMat);
            Imgproc.pyrDown(mPyrDownMat, mPyrDownMat);

            Imgproc.cvtColor(mPyrDownMat, mHsvMat, Imgproc.COLOR_RGB2HSV_FULL);
            Imgproc.cvtColor(mPyrDownMat, mRGBAMat, Imgproc.COLOR_RGB2RGBA);
            Imgproc.cvtColor(mPyrDownMat, mYCrCbMat, Imgproc.COLOR_RGB2YCrCb);

            Core.inRange(mHsvMat, fLowerBoundHSV, fUpperBoundHSV, fMaskHSV);

            fMask = fMaskHSV;

            Imgproc.dilate(fMask, fDilatedMask, new Mat());

            List<MatOfPoint> contoursFinger = new List<MatOfPoint>();

            Imgproc.findContours(fDilatedMask, contoursFinger, fHierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            if (contoursFinger.Count == 0)
            {
                FingerContour = null;
                return;
            }

            // Find max contour area
            double maxArea = 0;
            MatOfPoint biggestContour = null;
            foreach (MatOfPoint each in contoursFinger)
            {
                MatOfPoint wrapper = each;
                double area = Imgproc.contourArea(wrapper);
                if (area > maxArea)
                {
                    maxArea = area;
                    biggestContour = each;
                }
            } 
            if (maxArea < 130)
            {
                FingerContour = null;
                return;
            }

            //Debug.Log("Finger contour area" + maxArea.ToString());

            MatOfPoint2f contours_res2f = new MatOfPoint2f();

            MatOfPoint2f biggestContour2f = new MatOfPoint2f(biggestContour.toArray());
            Imgproc.approxPolyDP(biggestContour2f, contours_res2f, 3, true);
            FingerContour = new MatOfPoint(contours_res2f.toArray());
            contours_res2f.Dispose();
            biggestContour2f.Dispose();
            if (Imgproc.contourArea(FingerContour) > mMinContourArea * maxArea)
            {
                Core.multiply(FingerContour, new Scalar(4, 4), FingerContour);
            }
        }

        double ComputeAVGXForContour (MatOfPoint contour)
        {
            double sum = 0;
            Point[] points = contour.toArray();
            foreach (Point p in points)
            {
                sum += p.x;
            }
            return sum / points.Length;
        }

        public void ProcessSkin(Mat rgbaImage)
        {
            Imgproc.pyrDown(rgbaImage, mPyrDownMat);
            Imgproc.pyrDown(mPyrDownMat, mPyrDownMat);

            Imgproc.cvtColor(mPyrDownMat, mHsvMat, Imgproc.COLOR_RGB2HSV_FULL);
            Imgproc.cvtColor(mPyrDownMat, mRGBAMat, Imgproc.COLOR_RGB2RGBA);
            Imgproc.cvtColor(mPyrDownMat, mYCrCbMat, Imgproc.COLOR_RGB2YCrCb);

            Core.inRange(mHsvMat, mLowerBoundHSV, mUpperBoundHSV, mMaskHSV);
            Core.inRange(mPyrDownMat, mLowerBoundRGB, mUpperBoundRGB, mMaskRGB);
            Core.inRange(mYCrCbMat, mLowerBoundYCrCb, mUpperBoundYCrCb, mMaskYCrCb);

            mMask = mMaskYCrCb & mMaskHSV & mMaskRGB;

            Imgproc.dilate(mMask, mDilatedMask, new Mat());

            List<MatOfPoint> contours = new List<MatOfPoint>();

            Imgproc.findContours(mDilatedMask, contours, mHierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            if (contours.Count == 0)
            {
                return;
            }

            // Find max contour area
            double maxArea = 0;
            double secondMaxArea = 0;
            MatOfPoint biggestContour = null;
            MatOfPoint secondBiggestContour = null;
            foreach (MatOfPoint each in contours)
            {
                MatOfPoint wrapper = each;
                double area = Imgproc.contourArea(wrapper);
                if (area > maxArea)
                {
                    secondMaxArea = maxArea;
                    secondBiggestContour = biggestContour;

                    maxArea = area;
                    biggestContour = each;
                }
                else if (area > secondMaxArea)
                {
                    secondMaxArea = area;
                    secondBiggestContour = each;
                }
            }

            handContourSize = maxArea;

            if ((biggestContour != null) && (secondBiggestContour != null) && (ComputeAVGXForContour(biggestContour) >= ComputeAVGXForContour(secondBiggestContour)) && (secondMaxArea >= 0.3 * maxArea))
            {
                biggestContour = secondBiggestContour;
                handContourSize = secondMaxArea;
            }


            MatOfPoint2f contours_res2f = new MatOfPoint2f();

            MatOfPoint2f biggestContour2f = new MatOfPoint2f(biggestContour.toArray());
            Imgproc.approxPolyDP(biggestContour2f, contours_res2f, 3, true);
            handContour = new MatOfPoint(contours_res2f.toArray());
            contours_res2f.Dispose();
            biggestContour2f.Dispose();

            if (Imgproc.contourArea(handContour) > mMinContourArea * maxArea)
            {
                Core.multiply(handContour, new Scalar(4, 4), handContour);
            }

            // Filter contours by area and resize to fit the original image size
            mContours.Clear();

            foreach (MatOfPoint each in contours)
            {
                MatOfPoint contour = each;
                if (Imgproc.contourArea(contour) > mMinContourArea * maxArea)
                {
                    Core.multiply(contour, new Scalar(4, 4), contour);
                    mContours.Add(contour);
                }
            }
        }

        public List<MatOfPoint> GetContours()
        {
            return mContours;
        }

        public List<MatOfPoint> HandContours
        {
            get
            {
                return new List<MatOfPoint> { handContour };
            }
        }

        double handContourSize = -1;
        public double HandContourSize
        {
            get
            {
                return handContourSize;
            }
        }

        public MatOfPoint FingerContour { get; private set; }

        public void Dispose()
        {
            mSpectrum.Dispose();
            mPyrDownMat.Dispose();
            mHsvMat.Dispose();
            mMask.Dispose();
            mDilatedMask.Dispose();
            mHierarchy.Dispose();
        }
    }
}