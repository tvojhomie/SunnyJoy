#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
#import "UnityInterface.h"

@interface ECGPhotoPicker : NSObject <UIImagePickerControllerDelegate, UINavigationControllerDelegate,
    UIAdaptivePresentationControllerDelegate, UIPopoverPresentationControllerDelegate>
@property(nonatomic, copy) NSString *receiver;
@property(nonatomic, copy) NSString *callback;
@property(nonatomic, copy) NSString *destination;
@property(nonatomic, strong) UIImagePickerController *picker;
@property(nonatomic, assign) BOOL finished;
- (void)presentSource:(UIImagePickerControllerSourceType)source;
- (void)finishCancelled:(BOOL)cancelled error:(NSString *)error path:(NSString *)path;
@end

static ECGPhotoPicker *ECGActivePhotoPicker = nil;

static void ECGSendPhotoResult(NSString *receiver, NSString *callback, BOOL cancelled, NSString *error, NSString *path) {
    NSDictionary *payload = @{@"cancelled": @(cancelled), @"error": error ?: @"", @"path": path ?: @""};
    NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString *json = data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding]
        : @"{\"cancelled\":false,\"error\":\"The photo result could not be read.\",\"path\":\"\"}";
    UnitySendMessage(receiver.UTF8String, callback.UTF8String, json.UTF8String);
}

@implementation ECGPhotoPicker

- (void)presentSource:(UIImagePickerControllerSourceType)source {
    if (self.finished) return;
    if (![UIImagePickerController isSourceTypeAvailable:source]) {
        [self finishCancelled:NO error:@"This photo source is unavailable on this device." path:nil];
        return;
    }
    UIViewController *presenter = UnityGetGLViewController();
    while (presenter.presentedViewController && !presenter.presentedViewController.isBeingDismissed)
        presenter = presenter.presentedViewController;
    if (!presenter || !presenter.view.window || presenter.isBeingDismissed) {
        [self finishCancelled:NO error:@"Please try choosing a photo again." path:nil];
        return;
    }

    self.picker = [[UIImagePickerController alloc] init];
    self.picker.delegate = self;
    self.picker.sourceType = source;
    self.picker.mediaTypes = @[@"public.image"];
    self.picker.allowsEditing = NO; // The game owns its crop/preview step.
    if (source != UIImagePickerControllerSourceTypeCamera && UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad) {
        self.picker.modalPresentationStyle = UIModalPresentationPopover;
        UIPopoverPresentationController *popover = self.picker.popoverPresentationController;
        popover.sourceView = presenter.view;
        popover.sourceRect = CGRectMake(CGRectGetMidX(presenter.view.bounds), CGRectGetMidY(presenter.view.bounds), 1, 1);
        popover.permittedArrowDirections = 0;
        popover.delegate = self;
    } else {
        self.picker.modalPresentationStyle = UIModalPresentationFullScreen;
    }
    self.picker.presentationController.delegate = self;
    [presenter presentViewController:self.picker animated:YES completion:nil];
}

- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey, id> *)info {
    if (self.finished) return;
    UIImage *image = info[UIImagePickerControllerOriginalImage];
    if (![image isKindOfClass:UIImage.class] || image.size.width <= 0 || image.size.height <= 0) {
        [self finishCancelled:NO error:@"The selected photo could not be read." path:nil];
        return;
    }
    CGFloat ratio = MIN(1.0, 2048.0 / MAX(image.size.width, image.size.height));
    CGSize size = CGSizeMake(MAX(1, floor(image.size.width * ratio)), MAX(1, floor(image.size.height * ratio)));
    UIGraphicsImageRendererFormat *format = [UIGraphicsImageRendererFormat defaultFormat];
    format.scale = 1;
    format.opaque = YES;
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:size format:format];
    UIImage *normalized = [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        [UIColor.whiteColor setFill];
        UIRectFill(CGRectMake(0, 0, size.width, size.height));
        [image drawInRect:CGRectMake(0, 0, size.width, size.height)];
    }];
    NSData *jpeg = UIImageJPEGRepresentation(normalized, 0.9);
    NSError *error = nil;
    NSString *directory = self.destination.stringByDeletingLastPathComponent;
    BOOL directoryReady = [NSFileManager.defaultManager createDirectoryAtPath:directory
        withIntermediateDirectories:YES attributes:nil error:&error];
    BOOL saved = jpeg.length > 0 && directoryReady && [jpeg writeToFile:self.destination options:NSDataWritingAtomic error:&error];
    if (!saved) {
        [self finishCancelled:NO error:@"The photo could not be saved. Please try again." path:nil];
        return;
    }
    [self finishCancelled:NO error:nil path:self.destination];
}

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker {
    [self finishCancelled:YES error:nil path:nil];
}

- (void)presentationControllerDidDismiss:(UIPresentationController *)presentationController {
    [self finishCancelled:YES error:nil path:nil];
}

- (void)popoverPresentationControllerDidDismissPopover:(UIPopoverPresentationController *)popoverPresentationController {
    [self finishCancelled:YES error:nil path:nil];
}

- (void)finishCancelled:(BOOL)cancelled error:(NSString *)error path:(NSString *)path {
    if (self.finished) return;
    self.finished = YES;
    self.picker.delegate = nil;
    self.picker.presentationController.delegate = nil;
    self.picker.popoverPresentationController.delegate = nil;
    void (^complete)(void) = ^{
        ECGSendPhotoResult(self.receiver, self.callback, cancelled, error, path);
        self.picker = nil;
        if (ECGActivePhotoPicker == self) ECGActivePhotoPicker = nil;
    };
    if (self.picker.presentingViewController)
        [self.picker dismissViewControllerAnimated:YES completion:complete];
    else
        complete();
}

@end

static int ECGSourceAvailable(UIImagePickerControllerSourceType source) {
    __block BOOL available = NO;
    void (^check)(void) = ^{ available = [UIImagePickerController isSourceTypeAvailable:source]; };
    if (NSThread.isMainThread) check(); else dispatch_sync(dispatch_get_main_queue(), check);
    return available ? 1 : 0;
}

extern "C" int ECG_PhotoPicker_IsCameraSupported(void) {
    return ECGSourceAvailable(UIImagePickerControllerSourceTypeCamera);
}

extern "C" int ECG_PhotoPicker_IsLibrarySupported(void) {
    return ECGSourceAvailable(UIImagePickerControllerSourceTypePhotoLibrary);
}

extern "C" void ECG_PhotoPicker_Pick(int camera, const char *receiver, const char *callback, const char *destination) {
    NSString *receiverName = receiver ? [NSString stringWithUTF8String:receiver] : @"";
    NSString *methodName = callback ? [NSString stringWithUTF8String:callback] : @"";
    NSString *path = destination ? [NSString stringWithUTF8String:destination] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        if (ECGActivePhotoPicker) {
            ECGSendPhotoResult(receiverName, methodName, NO, @"A photo picker is already open.", nil);
            return;
        }
        ECGPhotoPicker *picker = [[ECGPhotoPicker alloc] init];
        picker.receiver = receiverName;
        picker.callback = methodName;
        picker.destination = path;
        ECGActivePhotoPicker = picker;
        if (!camera) {
            [picker presentSource:UIImagePickerControllerSourceTypePhotoLibrary];
            return;
        }
        if (![UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypeCamera]) {
            [picker finishCancelled:NO error:@"No camera is available on this device." path:nil];
            return;
        }
        AVAuthorizationStatus status = [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
        if (status == AVAuthorizationStatusAuthorized) {
            [picker presentSource:UIImagePickerControllerSourceTypeCamera];
        } else if (status == AVAuthorizationStatusNotDetermined) {
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (granted) [picker presentSource:UIImagePickerControllerSourceTypeCamera];
                    else [picker finishCancelled:NO error:@"Camera access was denied. You can allow it in iOS Settings." path:nil];
                });
            }];
        } else {
            [picker finishCancelled:NO error:@"Camera access is unavailable. Check the app's camera permission in iOS Settings." path:nil];
        }
    });
}
