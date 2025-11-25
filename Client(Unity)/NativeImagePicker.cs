using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

public class NativeImagePicker : MonoBehaviour
{
    // Windows API for file dialog
    #if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter = null;
        public string customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string file = null;
        public int maxFile = 0;
        public string fileTitle = null;
        public int maxFileTitle = 0;
        public string initialDir = null;
        public string title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
    #endif

    public static NativeImagePicker Instance { get; private set; }

    private Action<Texture2D> onImageSelected;
    private Action<string> onImageSelectionFailed;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 갤러리에서 이미지를 선택합니다.
    /// </summary>
    public void PickImageFromGallery(Action<Texture2D> onSuccess, Action<string> onError)
    {
        onImageSelected = onSuccess;
        onImageSelectionFailed = onError;
        
        #if UNITY_ANDROID && !UNITY_EDITOR
            PickImageAndroid();
        #elif UNITY_IOS && !UNITY_EDITOR
            PickImageiOS();
        #else
            // 에디터나 PC에서는 파일 다이얼로그 사용
            StartCoroutine(PickImageEditor());
        #endif
    }
    
    /// <summary>
    /// 카메라로 사진을 촬영합니다.
    /// </summary>
    public void TakePhoto(Action<Texture2D> onSuccess, Action<string> onError)
    {
        onImageSelected = onSuccess;
        onImageSelectionFailed = onError;
        
        #if UNITY_ANDROID && !UNITY_EDITOR
            TakePhotoAndroid();
        #elif UNITY_IOS && !UNITY_EDITOR
            TakePhotoiOS();
        #else
            // 에디터에서는 갤러리와 동일하게 처리
            StartCoroutine(PickImageEditor());
        #endif
    }
    
    #if UNITY_ANDROID && !UNITY_EDITOR
    void PickImageAndroid()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {

            if (path != null)
            {
                // 이미지를 Texture2D로 로드 (최대 크기 2048)
                Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 2048);

                if (texture != null)
                {
                    onImageSelected?.Invoke(texture);
                }
                else
                {
                    onImageSelectionFailed?.Invoke("Failed to load image");
                }
            }
            else
            {
                onImageSelectionFailed?.Invoke("No file selected");
            }
        }, "Select Profile Image");
    }

    void TakePhotoAndroid()
    {
        // NativeGallery 플러그인은 카메라 기능을 지원하지 않으므로 갤러리 선택으로 대체
        PickImageAndroid();
    }
    #endif
    
    #if UNITY_IOS && !UNITY_EDITOR
    void PickImageiOS()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {

            if (path != null)
            {
                // 이미지를 Texture2D로 로드 (최대 크기 2048)
                Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize: 2048);

                if (texture != null)
                {
                    onImageSelected?.Invoke(texture);
                }
                else
                {
                    onImageSelectionFailed?.Invoke("Failed to load image");
                }
            }
            else
            {
                onImageSelectionFailed?.Invoke("No file selected");
            }
        }, "Select Profile Image");
    }

    void TakePhotoiOS()
    {
        // NativeGallery 플러그인은 카메라 기능을 지원하지 않으므로 갤러리 선택으로 대체
        PickImageiOS();
    }
    #endif
    
    #if UNITY_EDITOR
    IEnumerator PickImageEditor()
    {
        // Unity 에디터에서는 간단한 파일 다이얼로그 사용
        // 기본 경로를 바탕화면으로 설정 (사용자가 변경 가능)
        string defaultPath = GetDefaultImagePath();
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Image", defaultPath, "png,jpg,jpeg", false);
        
        if (paths != null && paths.Length > 0)
        {
            string path = paths[0];
            if (File.Exists(path))
            {
                byte[] imageData = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                
                if (texture.LoadImage(imageData))
                {
                    onImageSelected?.Invoke(texture);
                }
                else
                {
                    onImageSelectionFailed?.Invoke("Failed to load image");
                }
            }
            else
            {
                onImageSelectionFailed?.Invoke("File not found");
            }
        }
        else
        {
            onImageSelectionFailed?.Invoke("No file selected");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// 기본 이미지 선택 경로를 반환합니다. 사용자가 커스터마이징 가능합니다.
    /// </summary>
    private string GetDefaultImagePath()
    {
        // 다음 중 하나를 선택하여 사용하세요:
        
        // 1. 바탕화면 경로
        return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        
        // 2. 내 문서 경로
        // return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        
        // 3. 다운로드 폴더 경로 (Windows)
        // return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
        
        // 4. 사진 폴더 경로
        // return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
        
        // 5. 사용자 지정 경로 (예시)
        // return @"C:\Users\YourUsername\Pictures";
        
        // 6. 빈 문자열 (시스템 기본값)
        // return "";
    }
    #elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
    IEnumerator PickImageEditor()
    {
        // Windows 빌드에서 파일 다이얼로그 사용
        string selectedPath = OpenFileDialog("Select Profile Image",
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures),
            "Image Files\0*.png;*.jpg;*.jpeg\0All Files\0*.*\0\0", "png");

        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
        {
            try
            {
                byte[] imageData = File.ReadAllBytes(selectedPath);
                Texture2D texture = new Texture2D(2, 2);

                if (texture.LoadImage(imageData))
                {
                    onImageSelected?.Invoke(texture);
                }
                else
                {
                    onImageSelectionFailed?.Invoke("Failed to load selected image");
                }
            }
            catch (System.Exception e)
            {
                onImageSelectionFailed?.Invoke($"Error loading image: {e.Message}");
            }
        }
        else
        {
            onImageSelectionFailed?.Invoke("No file selected");
        }

        yield return null;
    }

    private string OpenFileDialog(string title, string initialDirectory, string filter, string defaultExt)
    {
        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = filter;
        ofn.file = new string(new char[256]);
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string(new char[64]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = initialDirectory;
        ofn.title = title;
        ofn.defExt = defaultExt;
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008; // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXTENSIONDIFFERENT | OFN_NOCHANGEDIR

        if (GetOpenFileName(ofn))
        {
            return ofn.file;
        }
        return null;
    }
    #elif !UNITY_ANDROID && !UNITY_IOS && !UNITY_STANDALONE_WIN
    IEnumerator PickImageEditor()
    {
        // 다른 플랫폼에서는 기본 파일 찾기 방식 사용
        onImageSelectionFailed?.Invoke("File dialog not supported on this platform");
        yield return null;
    }
    #endif
    
    /// <summary>
    /// 네이티브 플러그인에서 호출되는 콜백 함수
    /// </summary>
    public void OnImageSelected(string base64String)
    {
        try
        {
            if (string.IsNullOrEmpty(base64String))
            {
                onImageSelectionFailed?.Invoke("No image data received");
                return;
            }
            
            byte[] imageData = Convert.FromBase64String(base64String);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(imageData))
            {
                onImageSelected?.Invoke(texture);
            }
            else
            {
                onImageSelectionFailed?.Invoke("Failed to decode image");
            }
        }
        catch (Exception e)
        {
            onImageSelectionFailed?.Invoke($"Error processing image: {e.Message}");
        }
    }
    
    /// <summary>
    /// 네이티브 플러그인에서 호출되는 오류 콜백 함수
    /// </summary>
    public void OnImageSelectionFailed(string errorMessage)
    {
        onImageSelectionFailed?.Invoke(errorMessage);
    }
    
    /// <summary>
    /// Texture2D를 바이트 배열로 변환 (PNG 형식)
    /// </summary>
    public static byte[] TextureToByteArray(Texture2D texture)
    {
        return texture.EncodeToPNG();
    }
    
    /// <summary>
    /// 이미지 크기 조절
    /// </summary>
    public static Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
    {
        float aspectRatio = (float)source.width / source.height;
        int newWidth, newHeight;
        
        if (source.width > source.height)
        {
            newWidth = Mathf.Min(maxWidth, source.width);
            newHeight = Mathf.RoundToInt(newWidth / aspectRatio);
        }
        else
        {
            newHeight = Mathf.Min(maxHeight, source.height);
            newWidth = Mathf.RoundToInt(newHeight * aspectRatio);
        }
        
        if (newWidth >= source.width && newHeight >= source.height)
        {
            return source;
        }
        
        RenderTexture renderTexture = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(source, renderTexture);
        
        RenderTexture.active = renderTexture;
        Texture2D result = new Texture2D(newWidth, newHeight);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);
        
        return result;
    }
}

// 에디터용 파일 브라우저 (간단한 구현)
#if UNITY_EDITOR
public static class StandaloneFileBrowser
{
    public static string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
    {
        string path = UnityEditor.EditorUtility.OpenFilePanel(title, directory, extension);
        if (string.IsNullOrEmpty(path))
            return new string[0];
        return new string[] { path };
    }
}
#endif