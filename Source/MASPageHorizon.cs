﻿/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPageHorizon : IMASMonitorComponent
    {
        private GameObject imageObject;
        private Material imageMaterial;
        private MeshRenderer meshRenderer;
        private readonly float textureOffset;
        private readonly Vector2 texelSize;
        private float pitchRange1, pitchRange2;
        private float displayPitchRange1, displayPitchRange2;
        private float rollRange1, rollRange2;
        private float displayRollRange1, displayRollRange2;
        private float lastRoll = 0.0f;
        private float oldPitchCenter = -1.0f;
        private Vector2 position = Vector2.zero;
        private Vector3 componentOrigin = Vector3.zero;

        internal MASPageHorizon(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in HORIZON " + name);
            }
            Texture2D mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for HORIZON " + name);
            }
            mainTexture.wrapMode = TextureWrapMode.Clamp;

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in HORIZON " + name);
            }

            string variableName = string.Empty;
            string pitchName = string.Empty;
            string rollName = string.Empty;
            if (!config.TryGetValue("pitch", ref pitchName))
            {
                throw new ArgumentException("Unable to find 'pitch' in HORIZON " + name);
            }

            string pitchRange = string.Empty;
            if (!config.TryGetValue("pitchRange", ref pitchRange))
            {
                throw new ArgumentException("Unable to find 'pitchRange' in HORIZON " + name);
            }
            string[] ranges = Utility.SplitVariableList(pitchRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'pitchRange' in HORIZON " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => pitchRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => pitchRange2 = (float)newValue);

            string displayPitchRange = string.Empty;
            if (!config.TryGetValue("displayPitchRange", ref displayPitchRange))
            {
                throw new ArgumentException("Unable to find 'displayPitchRange' in HORIZON " + name);
            }
            ranges = Utility.SplitVariableList(displayPitchRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'displayPitchRange' in HORIZON " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => displayPitchRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => displayPitchRange2 = (float)newValue);

            if (!config.TryGetValue("roll", ref rollName))
            {
                throw new ArgumentException("Unable to find 'roll' in HORIZON " + name);
            }

            string rollRange = string.Empty;
            if (!config.TryGetValue("rollRange", ref rollRange))
            {
                throw new ArgumentException("Unable to find 'rollRange' in HORIZON " + name);
            }
            ranges = Utility.SplitVariableList(rollRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'rollRange' in HORIZON " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => rollRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => rollRange2 = (float)newValue);

            string displayRollRange = string.Empty;
            if (!config.TryGetValue("displayRollRange", ref displayRollRange))
            {
                throw new ArgumentException("Unable to find 'displayRollRange' in HORIZON " + name);
            }
            ranges = Utility.SplitVariableList(displayRollRange);
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Incorrect number of values in 'displayRollRange' in HORIZON " + name);
            }
            variableRegistrar.RegisterVariableChangeCallback(ranges[0], (double newValue) => displayRollRange1 = (float)newValue);
            variableRegistrar.RegisterVariableChangeCallback(ranges[1], (double newValue) => displayRollRange2 = (float)newValue);

            texelSize = mainTexture.texelSize;

            // Infer the scaling of the source image to the display size, fixing
            // the width as 100% of the texel width of the source.
            float texelsPerPixel = (float)mainTexture.width / size.x;
            // Get the inverse aspect ratio of the display
            float aspectRatio = size.y / size.x;
            float texelsHeight = aspectRatio * size.y * texelsPerPixel;
            Vector2 textureScale = new Vector2(1.0f, texelsHeight * texelSize.y);
            textureOffset = 0.5f * texelsHeight * texelSize.y;

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            componentOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(position.x, -position.y, depth);

            // Determine the extents of the horizon in clip coordinates for
            // feeding the shader.
            Vector4 clipCoords = new Vector4(
                position.x - size.x * 0.5f, position.y - size.y * 0.5f,
                position.x + size.x * 0.5f, position.y + size.y * 0.5f
                );

            clipCoords.x /= monitor.screenSize.x * 0.5f;
            clipCoords.y /= monitor.screenSize.y * 0.5f;
            clipCoords.z /= monitor.screenSize.x * 0.5f;
            clipCoords.w /= monitor.screenSize.y * 0.5f;

            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x*0.75f, size.y*0.75f, depth),
                    new Vector3(size.x*0.75f, size.y*0.75f, depth),
                    new Vector3(-size.x*0.75f, -size.y*0.75f, depth),
                    new Vector3(size.x*0.75f, -size.y*0.75f, depth),
                };
            mesh.uv = new[]
                {
                    new Vector2(-0.5f, 1.5f),
                    new Vector2(1.5f, 1.5f),
                    new Vector2(-0.5f, -0.5f),
                    new Vector2(1.5f, -0.5f),
                };
            mesh.triangles = new[] 
                {
                    0, 1, 2,
                    1, 3, 2
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;
            Shader imageShader = MASLoader.shaders["MOARdV/Monitor"];
            imageMaterial = new Material(imageShader);
            imageMaterial.mainTexture = mainTexture;
            imageMaterial.mainTextureScale = textureScale;
            meshRenderer.material = imageMaterial;
            imageMaterial.SetVector("_ClipCoords", clipCoords);
            RenderPage(false);

            string positionString = string.Empty;
            if (!config.TryGetValue("position", ref positionString))
            {
                throw new ArgumentException("Unable to find 'position' in HORIZON " + name);
            }
            else
            {
                string[] pos = Utility.SplitVariableList(positionString);
                if (pos.Length != 2)
                {
                    throw new ArgumentException("Invalid number of values for 'position' in HORIZON " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(pos[0], (double newValue) =>
                {
                    position.x = (float)newValue;
                    imageObject.transform.position = componentOrigin + new Vector3(position.x, -position.y, 0.0f);

                    Vector4 clipC = new Vector4(
                position.x - size.x * 0.5f, position.y - size.y * 0.5f,
                position.x + size.x * 0.5f, position.y + size.y * 0.5f
                );

                    clipC.x /= monitor.screenSize.x * 0.5f;
                    clipC.y /= monitor.screenSize.y * 0.5f;
                    clipC.z /= monitor.screenSize.x * 0.5f;
                    clipC.w /= monitor.screenSize.y * 0.5f;
                    imageMaterial.SetVector("_ClipCoords", clipC);
                });

                variableRegistrar.RegisterVariableChangeCallback(pos[1], (double newValue) =>
                {
                    position.y = (float)newValue;
                    imageObject.transform.position = componentOrigin + new Vector3(position.x, -position.y, 0.0f);

                    Vector4 clipC = new Vector4(
                position.x - size.x * 0.5f, position.y - size.y * 0.5f,
                position.x + size.x * 0.5f, position.y + size.y * 0.5f
                );

                    clipC.x /= monitor.screenSize.x * 0.5f;
                    clipC.y /= monitor.screenSize.y * 0.5f;
                    clipC.z /= monitor.screenSize.x * 0.5f;
                    clipC.w /= monitor.screenSize.y * 0.5f;
                    imageMaterial.SetVector("_ClipCoords", clipC);
                });
            }

            variableRegistrar.RegisterVariableChangeCallback(pitchName, PitchCallback);
            variableRegistrar.RegisterVariableChangeCallback(rollName, RollCallback);
            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
            }
        }

        /// <summary>
        /// Update the texture roll.
        /// </summary>
        /// <param name="newValue"></param>
        private void RollCallback(double newValue)
        {
            float iLerp = Mathf.InverseLerp(rollRange1, rollRange2, (float)newValue);
            float newRoll = Mathf.Lerp(displayRollRange1, displayRollRange2, iLerp);

            if (!Mathf.Approximately(newRoll, lastRoll))
            {
                imageObject.transform.Rotate(Vector3.forward, newRoll - lastRoll);
                lastRoll = newRoll;
            }
        }

        /// <summary>
        /// Update the texture offset.  We do this be inverse-lerping the
        /// input variable and lerping it into the scaled output variable.
        /// </summary>
        /// <param name="newValue"></param>
        private void PitchCallback(double newValue)
        {
            float iLerp = Mathf.InverseLerp(pitchRange1, pitchRange2, (float)newValue);
            float newCenter = Mathf.Lerp(displayPitchRange1 * texelSize.y, displayPitchRange2 * texelSize.y, iLerp);

            if (!Mathf.Approximately(newCenter, oldPitchCenter))
            {
                imageMaterial.mainTextureOffset = new Vector2(0.0f, 1.0f - (newCenter + textureOffset));
                oldPitchCenter = newCenter;
            }
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
                imageObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public override void RenderPage(bool enable)
        {
            meshRenderer.enabled = enable;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
