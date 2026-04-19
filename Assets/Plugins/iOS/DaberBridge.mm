#import <Foundation/Foundation.h>

/**
 * Native bridge: Unity C# → React Native
 *
 * AvatarController.cs calls SendMessageToMobileApp via [DllImport("__Internal")].
 * This file provides the native C implementation that posts an NSNotification,
 * which the RN bridge picks up.
 */
extern "C" {
    void SendMessageToMobileApp(const char* message) {
        NSString *msgStr = [NSString stringWithUTF8String:message];
        NSLog(@"[Daber] Unity -> RN: %@", msgStr);
        [[NSNotificationCenter defaultCenter]
            postNotificationName:@"UnityMessageReceived"
            object:nil
            userInfo:@{@"message": msgStr}];
    }
}
