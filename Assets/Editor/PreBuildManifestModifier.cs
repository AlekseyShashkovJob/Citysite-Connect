using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Xml;

public class PreBuildManifestModifier : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        if (!File.Exists(manifestPath))
        {
            // Copy default manifest from Unity installation
            string unityManifestPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/Apk/AndroidManifest.xml");
            if (File.Exists(unityManifestPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                File.Copy(unityManifestPath, manifestPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Default Unity AndroidManifest.xml not found.");
                return;
            }
        }

        XmlDocument doc = new XmlDocument();
        doc.Load(manifestPath);

        XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("android", "http://schemas.android.com/apk/res/android");

        XmlNode applicationNode = doc.SelectSingleNode("/manifest/application");
        if (applicationNode != null)
        {
            XmlAttribute cleartextAttr = applicationNode.Attributes["android:usesCleartextTraffic"];
            if (cleartextAttr == null)
            {
                cleartextAttr = doc.CreateAttribute("android", "usesCleartextTraffic", "http://schemas.android.com/apk/res/android");
                cleartextAttr.Value = "true";
                applicationNode.Attributes.Append(cleartextAttr);
            }
            else
            {
                cleartextAttr.Value = "true";
            }
            doc.Save(manifestPath);
            UnityEngine.Debug.Log("Set android:usesCleartextTraffic=\"true\" in AndroidManifest.xml");
        }
        else
        {
            UnityEngine.Debug.LogWarning("<application> node not found in AndroidManifest.xml.");
        }
    }
}