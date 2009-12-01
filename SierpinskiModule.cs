/*
 * Copyright (c) Contributors http://github.com/aduffy70/Sierpinski-Tree
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Parallel Selves Chat Bridge nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace SierpinskiModule {
    public class SierpinskiModule : IRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>(); //our list of managed objects
		List<SceneObjectGroup> m_newprims = new List<SceneObjectGroup>(); //new prims to be added to the scene
        List<SceneObjectGroup> m_todelete = new List<SceneObjectGroup>(); //prims to be removed from the scene
        Random m_random = new Random();
        private Scene m_scene;

        // Adjust these parameters to control the size and location of the tree
        Vector3 m_pos = new Vector3(120f, 120f, 45f); //inworld coordinates for the center of the pyramid
		Vector3 m_size = new Vector3(30f, 30f, 40f); //dimensions of the pyramid in meters

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config) {
            m_log.Info("[SierpinskiModule] Initializing...");
            m_scene = scene;
        }

        public void PostInitialise() {
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
			InitializePyramid(m_scene);
        }

        public void Close(){
        }

        public string Name{
            get { return "SierpinskiModule"; }
        }

        public bool IsSharedModule {
            get { return false; }
        }

        #endregion

        void InitializePyramid(Scene scene) {
        	//Place one large pyramid prim of size size at position pos (you DO allow megaprims of at least size size in your region, right?
        	PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
        	prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f")); //give it a blank texture
            SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, m_pos, prim);
        	sog.RootPart.Scale = m_size;
        	m_prims.Add(sog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(sog, false);  //add it to the scene (not backed up to the db)
        }

        void OnChat(Object sender, OSChatMessage chat) {
        	if ((chat.Channel != 11) || (chat.Message.Length < 4)) {
                return;
            }
			else if (chat.Message.Substring(0,4) == "step") {
        		m_log.Info("[SierpinskiModule] Updating pyramid...");
        		foreach(SceneObjectGroup sog in m_prims) {
        			DoSierpinski(sog, m_size);
        		}
        		m_size = new Vector3(m_size.X / 2, m_size.Y / 2, m_size.Z /2);
                m_prims.Clear();
                m_log.Info("[SierpinskiModule] Pyramid contains " + m_newprims.Count + " prims");
                m_prims = new List<SceneObjectGroup>(m_newprims);
                foreach(SceneObjectGroup sog in m_todelete) {
                    m_scene.DeleteSceneObject(sog, false);
                }
                m_todelete.Clear();
                m_newprims.Clear();
        	}
        }

        void DoSierpinski(SceneObjectGroup sog, Vector3 scale) { //replace the original prim with 5 new prims
        	Vector3 newsize = new Vector3(scale.X / 2, scale.Y / 2, scale.Z / 2);
        	Vector3 offset = new Vector3(scale.X / 4, scale.Y / 4, scale.Z / 4);
        	Vector3 pos = sog.AbsolutePosition;
        	PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
        	prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
        	// Add new prim#1
            Vector3 newpos = new Vector3(pos.X, pos.Y, pos.Z + offset.Z);
        	SceneObjectGroup newsog = new SceneObjectGroup(UUID.Zero, newpos, prim);
        	newsog.RootPart.Scale = newsize;
            m_newprims.Add(newsog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog, false);  //add it to the scene (not backed up to the db)
            Primitive.TextureEntry tex = newsog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog.RootPart.UpdateTexture(tex);
        	// Add new prim#2
            prim.Textures.DefaultTexture.RGBA = RandomColor();
        	newpos = new Vector3(pos.X - offset.X, pos.Y - offset.Y, pos.Z - offset.Z);
        	newsog = new SceneObjectGroup(UUID.Zero, newpos, prim);
        	newsog.RootPart.Scale = newsize;
        	m_newprims.Add(newsog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog, false);  //add it to the scene (not backed up to the db)
            tex = newsog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog.RootPart.UpdateTexture(tex);
        	// Add new prim#3
            prim.Textures.DefaultTexture.RGBA = RandomColor();
        	newpos = new Vector3(pos.X - offset.X, pos.Y + offset.Y, pos.Z - offset.Z);
        	newsog = new SceneObjectGroup(UUID.Zero, newpos, prim);
        	newsog.RootPart.Scale = newsize;
        	m_newprims.Add(newsog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog, false);  //add it to the scene (not backed up to the db)
            tex = newsog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog.RootPart.UpdateTexture(tex);
			// Add new prim#4
            prim.Textures.DefaultTexture.RGBA = RandomColor();
        	newpos = new Vector3(pos.X + offset.X, pos.Y - offset.Y, pos.Z - offset.Z);
        	newsog = new SceneObjectGroup(UUID.Zero, newpos, prim);
        	newsog.RootPart.Scale = newsize;
            m_newprims.Add(newsog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog, false);  //add it to the scene (not backed up to the db)
            tex = newsog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog.RootPart.UpdateTexture(tex);
        	// Add new prim#5
            prim.Textures.DefaultTexture.RGBA = RandomColor();
        	newpos = new Vector3(pos.X + offset.X, pos.Y + offset.Y, pos.Z - offset.Z);
        	newsog = new SceneObjectGroup(UUID.Zero, newpos, prim);
        	newsog.RootPart.Scale = newsize;
        	m_newprims.Add(newsog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog, false);  //add it to the scene (not backed up to the db)
            tex = newsog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog.RootPart.UpdateTexture(tex);
        	m_todelete.Add(sog); //add the original prim to the list of prims to be removed
        }

        Color4 RandomColor() { //Randomly pick a color - but pick green more often than other colors
            Color4 randomcolor;
            int randomNumber = m_random.Next(0,10);
            if (randomNumber < 7) {
                randomcolor = new Color4(0.0f, 0.50f, 0.0f, 1.0f); //Green
            }
            else if (randomNumber == 7) {
                randomcolor = new Color4(1.0f, 0.0f, 0.0f, 1.0f); //Red
            }
            else if (randomNumber == 8) {
                randomcolor = new Color4(0.0f, 0.0f, 1.0f, 1.0f); //Blue
            }
            else {
                randomcolor = new Color4(1.0f, 1.0f, 0.0f, 1.0f); //Yellow
            }
            return randomcolor;
        }
    }
}
