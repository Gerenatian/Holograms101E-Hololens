using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.VR.WSA.Input;
using UnityEngine.VR.WSA.WebCam;
using UnityEngine.Windows.Speech;

public class SphereCommands : MonoBehaviour {
  Vector3 originalPosition;
  GestureRecognizer m_GestureRecognizer;
  PhotoCapture m_PhotoCaptureObj;
  CameraParameters m_CameraParameters;
  bool gestures = false;
  private TextMesh _holoText;
  private double UserAge = -1;


  // Use this for initialization
  void Start() {
    // Grab the original local position of the sphere when the app starts.
    originalPosition = this.transform.localPosition;

    Initialize();

    _holoText = GameObject.Find("AgeText").GetComponent<TextMesh>();
  }

  void Update() {
    if (UserAge != -1) {
      _holoText.text = "User is " + UserAge + " years old.";
    }
  }

  void SetupGestureRecognizer() {
    m_GestureRecognizer = new GestureRecognizer();
    m_GestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
    m_GestureRecognizer.TappedEvent += OnTappedEvent;
    m_GestureRecognizer.StartCapturingGestures();
  }

  void Initialize() {
    Debug.Log("Initializing...");

    List<Resolution> resolutions = new List<Resolution>(PhotoCapture.SupportedResolutions);
    Resolution selectedResolution = resolutions[0];

    m_CameraParameters = new CameraParameters(WebCamMode.PhotoMode);
    m_CameraParameters.cameraResolutionWidth = selectedResolution.width;
    m_CameraParameters.cameraResolutionHeight = selectedResolution.height;
    m_CameraParameters.hologramOpacity = 0.0f;
    m_CameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

    PhotoCapture.CreateAsync(false, OnCreatedPhotoCaptureObject);
  }

  void OnCreatedPhotoCaptureObject(PhotoCapture captureObject) {
    m_PhotoCaptureObj = captureObject;
    m_PhotoCaptureObj.StartPhotoModeAsync(m_CameraParameters, false, OnStartPhotoMode);
  }

  void OnStartPhotoMode(PhotoCapture.PhotoCaptureResult result) {
    if (!gestures) {
      SetupGestureRecognizer();
    }
  }

  void OnTappedEvent(InteractionSourceKind source, int tapCount, Ray headRay) {

    Debug.Log("Taking picture...");
    if (m_PhotoCaptureObj == null) {
      Initialize();
      return;
    }
    m_PhotoCaptureObj.TakePhotoAsync(OnCapturedPhotoToMemory);
  }

  // Called by GazeGestureManager when the user performs a Select gesture
  void OnSelect() {


  }

  void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame) {
    if (result.success) {
      List<byte> imageBufferList = new List<byte>();
      // Copy the raw IMFMediaBuffer data into our empty byte list.
      photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);
      Texture2D t2D = new Texture2D(1280, 720);
      photoCaptureFrame.UploadImageDataToTexture(t2D);
      var pngEncodedBytes = t2D.EncodeToPNG();


      WebRequest req = WebRequest.Create("https://api.projectoxford.ai/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=age");
      req.Method = "POST";
      req.Headers["Ocp-Apim-Subscription-Key"] = "0c172ba492ee480f8e69705766066da9";
      req.Headers["Content-Type"] = "application/octet-stream";
      var requestBody = pngEncodedBytes;

      // store the response in this
      string responseString = string.Empty;

      req.BeginGetRequestStream(ar => {
        var requestStream = req.EndGetRequestStream(ar);
        requestStream.Write(requestBody, 0, requestBody.Length);

        req.BeginGetResponse(a => {
          var response = req.EndGetResponse(a);
          var responseStream = response.GetResponseStream();
          using (var sr = new System.IO.StreamReader(responseStream)) {
            // read in the servers response right here.
            responseString = sr.ReadToEnd();

            // Workaround for issue with Unitys deserializer. The JsonUtility seems to only work with objects as root element.
            // Example of what MS spits back to demonstrate the issue. MS sends back array as root. 
            // [{"faceId":"806783ff-6e82-4240-8bfd-3943b6d3270d","faceRectangle":{"top":298,"left":789,"width":199,"height":199},"faceAttributes":{"age":34.5}}]
            string JSONToParse = "{\"UserData\":" + responseString + "}";

            UserIdentity jsonObject = JsonUtility.FromJson<UserIdentity>(JSONToParse);
            if (jsonObject != null) {

              UserAge = jsonObject.faceAttributes.age;
            }
          }
        }, null);

      }, null);
    }
    m_PhotoCaptureObj.StopPhotoModeAsync(OnStoppedPhotoMode);
  }

  void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result) {
    m_PhotoCaptureObj.Dispose();
    m_PhotoCaptureObj = null;
  }

  // Called by SpeechManager when the user says the "Reset world" command
  void OnReset() {
    // If the sphere has a Rigidbody component, remove it to disable physics.
    var rigidbody = this.GetComponent<Rigidbody>();
    if (rigidbody != null) {
      DestroyImmediate(rigidbody);
    }

    // Put the sphere back into its original local position.
    this.transform.localPosition = originalPosition;
  }

  // Called by SpeechManager when the user says the "Drop sphere" command
  void OnDrop() {
    // Just do the same logic as a Select gesture.
    OnSelect();
  }
}

[Serializable]
public class UserIdentity {
  public string faceId { get; set; }
  public FaceCoords faceRectangle = new FaceCoords();
  public FaceAttributes faceAttributes = new FaceAttributes();
}

public class FaceAttributes {
  public double age { get; set; }
}

public class FaceCoords {
  public int top { get; set; }
  public int left { get; set; }
  public int width { get; set; }
  public int height { get; set; }
}