﻿/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPageText : IMASMonitorComponent
    {
        private string text = string.Empty;

        private GameObject meshObject;
        private MdVTextMesh textObj;

        private Vector3 imageOrigin = Vector3.zero;
        private Vector2 position = Vector2.zero;
        private Vector2 fontScale;

        private object rpmModule;
        private Func<object, int, int, string> rpmModuleTextMethod;
        private MASFlightComputer comp;
        private InternalProp prop;

        internal MASPageText(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            if (!config.TryGetValue("text", ref text))
            {
                string textfile = string.Empty;
                if (!config.TryGetValue("textfile", ref textfile))
                {
                    string rpmModText = string.Empty;
                    if (!config.TryGetValue("textmethod", ref rpmModText))
                    {
                        throw new ArgumentException("Unable to find 'text', 'textfile', or 'textmethod' in TEXT " + name);
                    }

                    string[] rpmMod = rpmModText.Split(':');
                    if (rpmMod.Length != 2)
                    {
                        throw new ArgumentException("Invalid 'textmethod' in TEXT " + name);
                    }
                    bool moduleFound = false;

                    int numModules = prop.internalModules.Count;
                    int moduleIndex;
                    for (moduleIndex = 0; moduleIndex < numModules; ++moduleIndex)
                    {
                        if (prop.internalModules[moduleIndex].ClassName == rpmMod[0])
                        {
                            moduleFound = true;
                            break;
                        }
                    }

                    if (moduleFound)
                    {
                        rpmModule = prop.internalModules[moduleIndex];
                        Type moduleType = prop.internalModules[moduleIndex].GetType();
                        MethodInfo method = moduleType.GetMethod(rpmMod[1]);
                        if (method != null && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == typeof(int) && method.GetParameters()[1].ParameterType == typeof(int))
                        {
                            rpmModuleTextMethod = DynamicMethodFactory.CreateFunc<object, int, int, string>(method);
                        }
                    }

                    if (rpmModuleTextMethod != null)
                    {
                        this.comp = comp;
                        this.prop = prop;
                    }
                    text = " ";
                }
                else
                {
                    // Load text
                    text = string.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + textfile.Trim(), Encoding.UTF8));
                }
            }

            string localFonts = string.Empty;
            if (!config.TryGetValue("font", ref localFonts))
            {
                localFonts = string.Empty;
            }

            string styleStr = string.Empty;
            FontStyle style = FontStyle.Normal;
            if (config.TryGetValue("style", ref styleStr))
            {
                style = MdVTextMesh.FontStyle(styleStr);
            }
            else
            {
                style = monitor.defaultStyle;
            }

            Vector2 fontSize = Vector2.zero;
            if (!config.TryGetValue("fontSize", ref fontSize) || fontSize.x < 0.0f || fontSize.y < 0.0f)
            {
                fontSize = monitor.fontSize;
            }

            Color32 textColor;
            string textColorStr = string.Empty;
            if (!config.TryGetValue("textColor", ref textColorStr) || string.IsNullOrEmpty(textColorStr))
            {
                textColor = monitor.textColor_;
            }
            else
            {
                textColor = Utility.ParseColor32(textColorStr, comp);
            }

            // Position is based on default font size
            fontScale = monitor.fontSize;
            // Position is based on local font size.
            //fontScale = fontSize;

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            // Set up our text.
            imageOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            meshObject = new GameObject();
            meshObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            meshObject.layer = pageRoot.gameObject.layer;
            meshObject.transform.parent = pageRoot;
            meshObject.transform.position = imageOrigin;

            string positionString = string.Empty;
            if (config.TryGetValue("position", ref positionString))
            {
                string[] positions = Utility.SplitVariableList(positionString);
                if (positions.Length != 2)
                {
                    throw new ArgumentException("position does not contain 2 values in TEXT " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(positions[0], (double newValue) =>
                {
                    position.x = (float)newValue * fontScale.x;
                    meshObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                });

                variableRegistrar.RegisterVariableChangeCallback(positions[1], (double newValue) =>
                {
                    position.y = (float)newValue * fontScale.y;
                    meshObject.transform.position = imageOrigin + new Vector3(position.x, -position.y, 0.0f);
                });
            }

            textObj = meshObject.gameObject.AddComponent<MdVTextMesh>();

            Font font;
            if (string.IsNullOrEmpty(localFonts))
            {
                font = monitor.defaultFont;
            }
            else
            {
                font = MASLoader.GetFont(localFonts.Trim());
            }

            // We want to use a different shader for monitor displays.
            textObj.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
            textObj.SetFont(font, fontSize);
            textObj.SetColor(textColor);
            textObj.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
            textObj.fontStyle = style;

            // text, immutable, preserveWhitespace, comp, prop
            textObj.SetText(text, false, true, comp, prop);
            RenderPage(false);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                meshObject.SetActive(false);
                variableRegistrar.RegisterVariableChangeCallback(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
            }

            if (rpmModuleTextMethod != null)
            {
                comp.StartCoroutine(TextMethodUpdate());
            }
        }

        /// <summary>
        /// Check to see if the RPM module has updated its text.
        /// </summary>
        /// <returns></returns>
        private IEnumerator TextMethodUpdate()
        {
            string oldText = string.Empty;

            while (comp != null && prop != null)
            {
                // TODO: real values.
                if (currentState)
                {
                    string rv = rpmModuleTextMethod(rpmModule, 40, 32);
                    if (rv != oldText)
                    {
                        oldText = rv;
                        textObj.SetText(oldText, true, true, comp, prop);
                    }
                    //object rv = rpmModuleTextMethod(rpmModule, 40, 32);
                    //if (rv != null && (rv is string) && (rv as string) != oldText)
                    //{
                    //    oldText = rv as string;
                    //    textObj.SetText(oldText, true, true, comp, prop);
                    //}
                }
                yield return MASConfig.waitForFixedUpdate;
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
                meshObject.SetActive(currentState);
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
            textObj.SetRenderEnabled(enable);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            rpmModule = null;
            this.comp = null;
            this.prop = null;

            UnityEngine.GameObject.Destroy(meshObject);
            meshObject = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
