
//https://tesseract-ocr.github.io/tessdoc/APIExample
//https://www.learnopencv.com/deep-learning-based-text-recognition-ocr-using-tesseract-and-opencv/
//https://i.lu.je/2020/Discord_Har20uEPxu.png
#include <tesseract/baseapi.h>
#include <leptonica/allheaders.h>
#include <cmath>
#include <string>
#include <opencv2/opencv.hpp>
#include <opencv2/video.hpp>
#include <opencv2/videoio.hpp>
#include "ThreadPool.h"

using namespace std;
using namespace cv;

const int MAX_LEVENSHTEIN = 4;
const char *TESSERACT_PATH = NULL;

/** Creates a OCR object and reads the text off the image */
char* performOCR(Mat* image, tesseract::TessBaseAPI* ocr = NULL, int byteSize = 3) {

    bool didCreateOCR = false;

    //Create the OCR if we have none
    if (ocr == NULL) {
        ocr = new tesseract::TessBaseAPI();
        ocr->Init(TESSERACT_PATH, "eng", tesseract::OEM_LSTM_ONLY);
        ocr->SetPageSegMode(tesseract::PSM_SINGLE_BLOCK);
        didCreateOCR = true;
    }

    //Read the image data
    ocr->SetImage(image->data, image->cols, image->rows, byteSize, image->step);
    ocr->SetSourceResolution(70);

    auto text = ocr->GetUTF8Text();

    //Clear the OCR
    if (didCreateOCR) ocr->End();

    //Return text
    return text;
}

/** Performs Levenshtein Distancing */
size_t levenshtein_distance(const char* s, size_t n, const char* t, size_t m)
{
    ++n; ++m;
    size_t* d = new size_t[n * m];

    memset(d, 0, sizeof(size_t) * n * m);

    for (size_t i = 1, im = 0; i < m; ++i, ++im)
    {
        for (size_t j = 1, jn = 0; j < n; ++j, ++jn)
        {
            if (s[jn] == t[im])
            {
                d[(i * n) + j] = d[((i - 1) * n) + (j - 1)];
            }
            else
            {
                d[(i * n) + j] = min(d[(i - 1) * n + j] + 1, /* A deletion. */
                    min(d[i * n + (j - 1)] + 1, /* An insertion. */
                        d[(i - 1) * n + (j - 1)] + 1)); /* A substitution. */
            }
        }
    }

    size_t r = d[n * m - 1];
    delete[] d;
    return r;
}

struct ProcessData {
    Mat frame;
    unsigned long framecount;
    int threshold_value;
    double fps;
    const char* name;
};

void process_frame(ProcessData data) {

    const bool debug_display = false;
    bool shouldOCR = false;

    //Prepare the frame
    cv::resize(data.frame, data.frame, Size(398, 53));

    //Threshold the image
    Mat grey, thresh, result;
    cv::cvtColor(data.frame, grey, cv::COLOR_BGR2GRAY);
    cv::threshold(grey, thresh, 90, 255, cv::THRESH_BINARY);

    //Draw the contors
    std::vector<std::vector<cv::Point> > contours;
    cv::findContours(thresh, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
    drawContours(thresh, contours, -1, Scalar(180, 180, 180), 1);

    std::vector<cv::Point> approx;
    for (int i = 0; i < contours.size(); i++)
    {
        // Approximate contour with accuracy proportional
        // to the contour perimeter
        cv::approxPolyDP(cv::Mat(contours[i]), approx, cv::arcLength(cv::Mat(contours[i]), true) * 0.02, true);

        // Skip small or non-convex objects 
        if (std::fabs(cv::contourArea(contours[i])) < 100 || !cv::isContourConvex(approx))
            continue;

        //Get the bounds
        cv::Rect r = cv::boundingRect(contours[i]);

        //Validate the height is correct
        if (r.height < 50) {

            if (debug_display) {
                cv::rectangle(data.frame, r, Scalar(255, 255, 0), 1);
            }

            //Validate we are within bounds
            if (r.x > 120 || (r.x + r.width) > 120) {
                if (debug_display) {
                    cv::rectangle(data.frame, r, Scalar(255, 0, 0), 2);
                }

                shouldOCR = true;
                break;
            }
        }
    }

    if (shouldOCR)
    {

        //Theshhold the OCR image
        cv::threshold(grey, thresh, data.threshold_value, 215, cv::THRESH_BINARY);

        //Perform the OCR
        auto res = performOCR(&thresh, NULL, 1);
        size_t src_len = strlen(data.name);
        size_t res_len = strlen(res);

        //If we are enough, then display
        if (debug_display || res_len >= src_len) {
            int levenshtein = levenshtein_distance(res, src_len > res_len ? res_len : src_len, data.name, src_len);

            //Log the deets
            if (debug_display || levenshtein <= MAX_LEVENSHTEIN) {
                cout << "frame = " << to_string(data.framecount) << "@" << to_string(data.fps) << ";";
                cout << "time = " << to_string(data.framecount / data.fps) << ";";
                cout << "levenshtein = " << to_string(levenshtein) << ";";
                cout << "text = " << res << ";" << endl;
            }
        }
    }
}

int main(int argc, char* argv[])
{
    auto start = std::chrono::high_resolution_clock::now();

    //const std::string url = "https://mixer.com/api/v1/channels/55075068/manifest.m3u8";   //Lachee
    //const std::string url = "https://mixer.com/api/v1/channels/83229758/manifest.m3u8";   //Ondo
    const std::string url = argv[2];

    int frameRatio = 120;           //how many frames between each check
    int maxLevenshtein = 4;            //max levenshtein
    const char* name = argv[1];      //Name to detect
    unsigned long framecount = 0;

    //Create a thread pool
    ThreadPool pool(16);
#ifdef DEBUG
    const bool debug_display = true;
    framecount = 6000;
#else
    const bool debug_display = false;
#endif

    //Prepare threshold
    int threshold_value = 180;

    //Prepare the video
    cv::VideoCapture capture(url);
    double fps = capture.get(cv::CAP_PROP_FPS);
    bool stream_enabled = true;

    //Prepare OCR
    //auto ocr = new tesseract::TessBaseAPI();
    //ocr->Init(TESSERACT_PATH, "eng", tesseract::OEM_DEFAULT);

    //Did we failed?
    if (!capture.isOpened()) {
        stream_enabled = false;
    }

    //Prepare the frame
    cv::Mat frame;
    capture.set(cv::CAP_PROP_POS_FRAMES, framecount);

    std::future<void> future;

    while (stream_enabled) {
        //Count the frame
        framecount++;

        //If we have not met our ratio, grab the frame but dont do anything
        if (framecount % frameRatio != 0) {
            capture.grab();
            continue;
        }

        //We hit our freshhold, log it
        if (!capture.read(frame)) {
            stream_enabled = false;
            continue;
        }

        //Enqueue the frame
        ProcessData data;
        data.frame = frame;
        data.fps = fps;
        data.framecount = framecount;
        data.name = name;
        data.threshold_value = threshold_value;
        future = pool.enqueue(process_frame, data);
    }

    //Finish the OCR
    //ocr->End();

    //Wait for the last future
    future.wait();

    auto finish = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double> elapsed = finish - start;
    std::cout << "duration = " << to_string(elapsed.count()) << ";" << endl;
    return EXIT_SUCCESS;
}
