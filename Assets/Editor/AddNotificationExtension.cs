#if !SHORT_BUILD
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Text;

public static class AddNotificationExtension
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

#if UNITY_2019_3_OR_NEWER
        string mainTarget = project.GetUnityMainTargetGuid();
#else
        string mainTarget = project.TargetGuidByName("Unity-iPhone");
#endif
        
        project.SetBuildProperty(mainTarget, "SWIFT_VERSION", "5.0");
        project.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        project.SetBuildProperty(mainTarget, "EMBEDDED_CONTENT_CONTAINS_SWIFT", "YES");
        project.AddBuildProperty(mainTarget, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
        project.AddBuildProperty(mainTarget, "OTHER_LDFLAGS", "-ObjC");
        project.AddFrameworkToProject(mainTarget, "UserNotifications.framework", false);

        string extGuid = CreateNotificationExtension(project, buildPath, mainTarget);
        
        AddEntitlements(project, buildPath, mainTarget);
        UpdateMainInfoPlist(buildPath);
        GeneratePodfile(buildPath);

        project.WriteToFile(projectPath);
    }
    
    private static string CreateNotificationExtension(PBXProject project, string buildPath, string mainTarget)
    {
            const string extName = "notifications";
            string relFolder = "notifications";
            Directory.CreateDirectory(Path.Combine(buildPath, relFolder));

            string relSwiftPath = Path.Combine(relFolder, "NotificationService.swift").Replace("\\", "/");
            string relPlistPath = Path.Combine(relFolder, "Info.plist").Replace("\\", "/");
            string relEntitlementsPath = Path.Combine(relFolder, "notifications.entitlements").Replace("\\", "/");

            string absSwiftPath = Path.Combine(buildPath, relSwiftPath);
            string absPlistPath = Path.Combine(buildPath, relPlistPath);
            string absEntitlementsPath = Path.Combine(buildPath, relEntitlementsPath);

            File.WriteAllText(absSwiftPath, GenerateSwiftServiceClass());
            File.WriteAllText(absPlistPath, GenerateNotificationPlist());
            File.WriteAllText(absEntitlementsPath, GenerateEntitlementsPlist());
            
            string swiftGuid = project.AddFile(relSwiftPath, relSwiftPath, PBXSourceTree.Source);
            project.AddFile(relPlistPath, relPlistPath, PBXSourceTree.Source);
            project.AddFile(relEntitlementsPath, relEntitlementsPath, PBXSourceTree.Source);

            string extGuid = project.AddTarget(extName, extName, "com.apple.product-type.app-extension");
            string srcPhase = project.GetSourcesBuildPhaseByTarget(extGuid);
            if (string.IsNullOrEmpty(srcPhase))
                srcPhase = project.AddSourcesBuildPhase(extGuid);
            project.AddFileToBuild(extGuid, swiftGuid);

            project.SetBuildProperty(extGuid, "INFOPLIST_FILE", relPlistPath);
            project.SetBuildProperty(extGuid, "PRODUCT_NAME", extName);
            
            project.SetBuildProperty(extGuid, "PRODUCT_BUNDLE_IDENTIFIER",
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS) + ".notifications");
            project.SetBuildProperty(extGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(extGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.SetBuildProperty(extGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
            project.SetBuildProperty(extGuid, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildProperty(extGuid, "CODE_SIGN_ENTITLEMENTS", relEntitlementsPath);

            project.AddTargetDependency(mainTarget, extGuid);

            return extGuid;
    }

    private static void AddEntitlements(PBXProject project, string buildPath, string mainTarget)
    {
        string sourceEnt = Path.Combine(buildPath, "notifications/notifications.entitlements");
        string mainEnt   = Path.Combine(buildPath, "main.entitlements");
        File.Copy(sourceEnt, mainEnt, true);

        var capManager = new ProjectCapabilityManager(PBXProject.GetPBXProjectPath(buildPath), "main.entitlements", null, mainTarget);
        capManager.AddPushNotifications(false);
        capManager.WriteToFile();
        project.SetBuildProperty(mainTarget, "CODE_SIGN_ENTITLEMENTS", "main.entitlements");
    }

    private static void UpdateMainInfoPlist(string buildPath)
    {
        string plist = Path.Combine(buildPath, "Info.plist");
        var doc = new PlistDocument();
        doc.ReadFromFile(plist);
        var root = doc.root;

        root.SetString("NSPhotoLibraryUsageDescription", "Allows photo library access.");
        root.SetString("NSCameraUsageDescription", "Allows camera access.");
        root.SetString("NSMicrophoneUsageDescription", "Allows microphone access.");
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        var ats = root.CreateDict("NSAppTransportSecurity");
        ats.SetBoolean("NSAllowsArbitraryLoads", true);
        ats.SetBoolean("NSAllowsArbitraryLoadsInWebContent", true);
        ats.SetBoolean("NSAllowsLocalNetworking", true);
        ats.SetBoolean("NSAllowsArbitraryLoadsForMedia", true);

        File.WriteAllText(plist, doc.WriteToString());
    }

    private static void GeneratePodfile(string buildPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("source 'https://cdn.cocoapods.org/'");
        sb.AppendLine("platform :ios, '15.0'");
        sb.AppendLine();
        sb.AppendLine("use_frameworks!");
        sb.AppendLine();
        sb.AppendLine("target 'UnityFramework' do");
        sb.AppendLine("  pod 'AppsFlyerFramework', '6.17.8'");
        sb.AppendLine("  pod 'PurchaseConnector', '6.17.8'");        
        sb.AppendLine("  pod 'FirebaseMessaging', '11.10.0'");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine("target 'Unity-iPhone' do");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine("target 'notifications' do");
        sb.AppendLine("  pod 'FirebaseMessaging', '11.10.0'");
        sb.AppendLine("end");
        
        File.WriteAllText(Path.Combine(buildPath, "Podfile"), sb.ToString());
        File.WriteAllText(Path.Combine(buildPath, "podfile_ready"), "ok");
    }

    private static string GenerateNotificationPlist()
    {
        string bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS) + ".notifications";
        string displayName = "notifications"; // Можно кастомно

        return $@"<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE plist PUBLIC '-//Apple//DTD PLIST 1.0//EN' 'http://www.apple.com/DTDs/PropertyList-1.0.dtd'>
<plist version='1.0'>
<dict>
  <key>CFBundleIdentifier</key>
  <string>{bundleId}</string>
  <key>CFBundleDisplayName</key>
  <string>{displayName}</string>
  <key>CFBundleName</key>
  <string>{displayName}</string>
  <key>CFBundleExecutable</key>
  <string>{displayName}</string>
  <key>NSExtension</key>
  <dict>
    <key>NSExtensionPointIdentifier</key>
    <string>com.apple.usernotifications.service</string>
    <key>NSExtensionPrincipalClass</key>
    <string>$(PRODUCT_MODULE_NAME).NotificationService</string>
  </dict>
</dict>
</plist>";
    }

    private static string GenerateEntitlementsPlist()
    {
        return @"<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE plist PUBLIC '-//Apple//DTD PLIST 1.0//EN' 'http://www.apple.com/DTDs/PropertyList-1.0.dtd'>
<plist version='1.0'>
<dict>
  <key>aps-environment</key>
  <string>production</string>
</dict>
</plist>";
    }

    private static string GenerateSwiftServiceClass()
    {
        return @"import UserNotifications
import FirebaseMessaging

class NotificationService: UNNotificationServiceExtension {
    var contentHandler: ((UNNotificationContent) -> Void)?
    var bestAttemptContent: UNMutableNotificationContent?
    
    override func didReceive(_ request: UNNotificationRequest, withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void) {
        self.contentHandler = contentHandler
        bestAttemptContent = request.content.mutableCopy() as? UNMutableNotificationContent
        
        guard let bestAttemptContent = bestAttemptContent else { return }
        
        FIRMessagingExtensionHelper().populateNotificationContent(
            bestAttemptContent,
            withContentHandler: contentHandler)
    }
    
    override func serviceExtensionTimeWillExpire() {
        // Called just before the extension will be terminated by the system.
        // Use this as an opportunity to deliver your ""best attempt"" at modified content, otherwise the original push payload will be used.
        if let contentHandler = contentHandler, let bestAttemptContent = bestAttemptContent {
            contentHandler(bestAttemptContent)
        }
    }
}";
    }
}
#endif