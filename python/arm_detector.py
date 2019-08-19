import cv2
import numpy as np
import math
import os

# colors declaration
# buttons color
# blue_color = np.array([243, 160, 65])
# red_color = np.array([19, 78, 238])
blue_color_low = np.array([240, 160, 65])
blue_color_high = np.array([245, 167, 75])
red_color_low = np.array([17, 78, 238])
red_color_high = np.array([20, 78, 238])
# finger (green) colors
min_HSV_finger = np.array([60, 50, 50])
max_HSV_finger = np.array([116, 255, 255])
# skin colors
min_HSV = np.array([0, 50, 0])
max_HSV = np.array([35, 255, 255])
min_YCrCb = np.array([0, 130, 67])
max_YCrCb = np.array([255, 173, 133])
min_RGB = np.array([0, 0, 0])
max_RGB = np.array([255, 255, 150])


def nearest_point(arm_contour, arm_angle, finger_contour):
    res = (-1, -1)
    # min_dist = -1
    # deltaFor90Deg = 10
    if not finger_contour.any():
        return res
    sorted_contour = np.sort(finger_contour, 1)
    highest_p = sorted_contour[0]
    return np.squeeze(highest_p)


def compute_contour(mask):  #, bgr_image):
    contours, _ = cv2.findContours(mask, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

    if not contours:
        return

    # find max contour area
    max_area = 0
    sec_max_area = 0
    biggest_contour = None
    for contour in contours:
        area = cv2.contourArea(contour)
        if area > max_area:
            sec_max_area = max_area
            max_area = area
            biggest_contour = contour
        elif area > sec_max_area:
            sec_max_area = area

    #cv2.drawContours(bgr_image, [biggest_contour], -1, 255, -1)

    moments = cv2.moments(biggest_contour)
    center = None
    if moments['m00'] != 0:
        cx = int(moments['m10']/moments['m00'])  # cx = M10/M00
        cy = int(moments['m01']/moments['m00'])  # cy = M01/M00
        center = (cx, cy)

    #cv2.circle(bgr_image, center, 7, [100, 200, 200], 2)

    angle = cv2.fitEllipse(biggest_contour)[-1]  # angle is the last param in ellipse

    return biggest_contour, center, angle


def process_finger(bgr_image):

    image_HSV = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2HSV)
    mask_HSV = cv2.inRange(image_HSV, min_HSV_finger, max_HSV_finger)
    if np.any(mask_HSV):
        contour, center, angle = compute_contour(mask_HSV)
    else:
        return None, (-1, -1), -1, None

    # cv2.drawContours(bgr_image, [contour], -1, 255, -1)
    # cv2.imwrite('arm_new.jpg', image)

    print("finger angle: " + str(angle))
    return contour, center, angle, mask_HSV


# cv2 reads images as BGR
def process_skin(bgr_image):
    image_RGB = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2RGB)
    image_HSV = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2HSV)
    image_YCrCb = cv2.cvtColor(bgr_image,cv2.COLOR_BGR2YCrCb)

    skin_YCrCb = cv2.inRange(image_YCrCb, min_YCrCb, max_YCrCb)
    skin_HSV = cv2.inRange(image_HSV, min_HSV, max_HSV)
    skin_RGB = cv2.inRange(image_RGB, min_RGB, max_RGB)

    skin_mask = skin_YCrCb & skin_HSV & skin_RGB

    contour, center, angle = compute_contour(skin_mask)

    # cv2.drawContours(bgr_image, [contour], -1, 255, -1)
    # cv2.imwrite('arm_new.jpg', image)

    print("arm angle: " + str(angle))

    return contour, center, angle


def check_pressed_button(image_menu_nmergerd, touch_point, color):
    if color == "blue":
        color_low = blue_color_low
        color_high = blue_color_high
    else:
        color_low = red_color_low
        color_high = red_color_high
    mask_color = cv2.inRange(image_menu_nmergerd, color_low, color_high)
    if mask_color[touch_point[1], touch_point[0]] > 0:
        print("pressed "+color)


#image = cv2.imread("images/hand5.jpg")
def process_image(image_name, folder_src, folder_dst, alpha, pressed_color, fade_in=True):  # image is the path for image
    #image = cv2.imread("images/hand5.jpg")
    #image = cv2.imread("images/hand45.jpg")
    #image = cv2.imread("images/hand5.jpg")
    pressed_red = pressed_blue = False

    image_path = os.path.join(folder_src,image_name)
    image = cv2.imread(image_path)

    image2 = image
    image = cv2.GaussianBlur(image, (3, 3), 1, 1)


    arm_contour, arm_center, arm_angle = process_skin(image)
    finger_contour, finger_center, finger_angle, mask_finger = process_finger(image)

    rotated = imutils.rotate_bound(menu, arm_angle)

    if arm_angle > 45:
        x_offset = arm_center[0] + math.floor(rotated.shape[1] / 2)
        y_offset = arm_center[1] - math.floor(rotated.shape[0] / 2)
        image[y_offset:y_offset+rotated.shape[0], x_offset-rotated.shape[1]:x_offset] = rotated
    else:
        x_offset = arm_center[0] - math.floor(rotated.shape[1] / 2)
        y_offset = arm_center[1] - math.floor(rotated.shape[0] / 2)
        image[y_offset:y_offset+rotated.shape[0], x_offset:x_offset+rotated.shape[1]] = rotated

    image_menu_nmergerd = image
    image = np.maximum(image2, image)
    #cv2.circle(image, arm_center, 7, [100, 200, 200], 5)

    # finger is in
    if finger_angle != -1:
        touch_point = nearest_point(arm_contour, arm_angle, finger_contour)
        pressed_blue = check_pressed_button(image_menu_nmergerd, touch_point, "blue")
        pressed_red = check_pressed_button(image_menu_nmergerd, touch_point, "red")
        image[mask_finger == 255] = image2[mask_finger == 255]
        cv2.circle(image, (touch_point[0], touch_point[1]), 7, [100, 200, 200], 7)
        if pressed_blue:
            pressed_color = (255, 0, 0)
        if pressed_red:
            pressed_color = (0, 0, 255)

    if pressed_blue or pressed_red or alpha > 0:
        overlay = image.copy()
        cv2.putText(overlay, "BUTTON PRESSED",(10, 100), cv2.FONT_HERSHEY_TRIPLEX, 4.0, pressed_color, 6)
        cv2.addWeighted(overlay, alpha, image, 1 - alpha, 0, image)
        if fade_in:
            alpha += 0.05
        else:
            alpha -= 0.05
        if alpha >= 1:
            fade_in = False
        if alpha <= 0:
            fade_in = True

    image_path = os.path.join(folder_dst, image_name)
    cv2.imwrite(image_path, image)
    return alpha, fade_in, pressed_color


menu = cv2.imread("menu_color_changer_small.png", 1)
scale = 0.3
menu = cv2.resize(menu,(math.floor(menu.shape[1]*scale),math.floor(menu.shape[0]*scale)))
folder = 'C:\\Users\\Tom\\Documents\\Learning_stuff\\semester 8\\AR_project\\video'
folder_src = folder+'\\ortal_in'
folder_dst = folder+'\\out'
# print(folder_src)
# count = 0
alpha = 0
fade_in = True
pressed_color = (0, 0, 0)
images_list = os.listdir(folder_src)
for image in images_list:
    alpha, fade_in, pressed_color = process_image(image, folder_src, folder_dst, alpha, pressed_color, fade_in)
    # count += 1
    # if count == 1:
    #     exit()
