﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using IcomMediaDisplay.Helpers;
using MEC;
using Time = UnityEngine;

namespace IcomMediaDisplay.Logic
{
    public class PlaybackHandler
    {
        private int currentFrameIndex = 0;
        private string[] frames;
        private float targetFrameDurationInSeconds;
        public int VideoFps = 30;

        public void PlayFrames(string folderPath)
        {
            Log.Debug("Called PlayFrames");
            string[] unsortedFrames = Directory.GetFiles(folderPath, "*.png");

            frames = unsortedFrames
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                .ToArray();

            if (frames.Length == 0)
            {
                Log.Error("No frames found in the specified folder.");
                return;
            }

            // Set the target frame duration
            targetFrameDurationInSeconds = 1.0f / VideoFps;

            // Start coroutine to play frames
            Timing.RunCoroutine(PlayFramesCoroutine());
        }

        private IEnumerator<float> PlayFramesCoroutine()
        {
            var startTime = Time.Time.time; // Record the starting time

            while (currentFrameIndex < frames.Length)
            {
                string framePath = frames[currentFrameIndex];
                Log.Debug($"Displaying frame: {framePath}");

                try
                {
                    using (FileStream stream = new FileStream(framePath, FileMode.Open, FileAccess.Read))
                    using (Bitmap frame = new Bitmap(stream))
                    {
                        string tmpRepresentation = FrameTMP(frame);
                        Intercom.IntercomDisplay.Network_overrideText = tmpRepresentation;
                        Log.Debug($"Frame displayed. Text length: {tmpRepresentation.Length}");
                        currentFrameIndex++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error displaying frame: {framePath}. Details: {ex}");
                    currentFrameIndex++; // Proceed to the next frame even if an error occurs
                }

                var elapsedTime = Time.Time.time - startTime; // Calculate elapsed time
                var targetTime = currentFrameIndex * targetFrameDurationInSeconds;
                var delayTime = targetTime - elapsedTime;

                if (delayTime > 0)
                    yield return Timing.WaitForSeconds(delayTime);
                else
                    yield return 0; // No delay if the target time has already passed
            }
        }

        public string FrameTMP(Bitmap frame)
        {
            int height = frame.Height;
            int width = frame.Width;
            StringBuilder codeBuilder = new StringBuilder();

            Color previousColor = Color.Empty; // Track the previous color
            StringBuilder colorBlock = new StringBuilder();

            for (int y = 0; y < height; y++)
            {
                colorBlock.Clear(); // Clear the color block for each new row
                previousColor = Color.Empty; // Reset previous color for new row

                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = frame.GetPixel(x, y);

                    if (pixelColor != previousColor)
                    {
                        if (colorBlock.Length > 0)
                        {
                            codeBuilder.Append(GetColoredBlock(colorBlock.ToString(), previousColor));
                            colorBlock.Clear();
                        }
                    }

                    colorBlock.Append(IcomMediaDisplay.instance.Config.Pixel);
                    previousColor = pixelColor;
                }

                codeBuilder.Append(GetColoredBlock(colorBlock.ToString(), previousColor)).Append("\n");
            }

            string codeStr = IcomMediaDisplay.instance.Config.Height + codeBuilder.ToString();
            string done = Compressors.CompressTMP(codeStr);
            Log.Debug($"Frame Converted, length is {done.Length}");
            return done;
        }

        private string GetColoredBlock(string content, Color color)
        {
            string hexValue = "#" + Converters.RgbToHex(color);
            return $"<color={hexValue}>{content}</color>";
        }
    }
}