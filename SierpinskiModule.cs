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
 *     * Neither the name of the Sierpinski-Tree module nor the
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

namespace SierpinskiModule
{
    public class SierpinskiModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		IDialogModule m_dialogmod;
        List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>(); //our list of managed objects
		List<SceneObjectGroup> m_newprims = new List<SceneObjectGroup>(); //new prims to be added to the scene
        Random m_random = new Random();
        bool m_enabled = false;
        int m_channel = 11;
        private Scene m_scene;
        bool m_isHidden = true; //tracks whether pyramid is hidden or shown
        Vector3 m_pos = new Vector3(128f, 128f, 40f); //inworld coordinates for the center of the pyramid
		float m_xSize;
        float m_ySize;
        float m_zSize;
        Vector3 m_size = new Vector3(30f, 30f, 40f); //dimensions of the pyramid in meters

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig sierpinskiTreeConfig = config.Configs["SierpinskiTree"];
            if (sierpinskiTreeConfig != null)
            {
                m_enabled = sierpinskiTreeConfig.GetBoolean("enabled", false);
                m_channel = sierpinskiTreeConfig.GetInt("chat_channel", 11);
                float xPos = sierpinskiTreeConfig.GetFloat("tree_x_position", 128);
                float yPos = sierpinskiTreeConfig.GetFloat("tree_y_position", 128);
                float zPos = sierpinskiTreeConfig.GetFloat("tree_z_position", 50);
                m_pos = new Vector3(xPos, yPos, zPos);
                m_xSize = sierpinskiTreeConfig.GetFloat("tree_x_size", 30);
                m_ySize = sierpinskiTreeConfig.GetFloat("tree_y_size", 30);
                m_zSize = sierpinskiTreeConfig.GetFloat("tree_z_size", 40);
            }
            if (m_enabled)
            {
                m_log.Info("[SierpinskiTreeModule] Initializing...");
                m_dialogmod = scene.RequestModuleInterface<IDialogModule>();
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "SierpinskiTreeModule";
            }
        }

        public bool IsSharedModule
        {
            get
            {
                return false;
            }
        }

        #endregion

        void InitializePyramid(Scene scene)
        {
        	//Place one large pyramid prim of size size at position pos
        	PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
        	prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f")); //give it a blank texture
            SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, m_pos, prim);
            Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = new Color4(0.0f, 0.50f, 0.0f, 1.0f); //Green
            sog.RootPart.UpdateTexture(tex);
            m_size = new Vector3(m_xSize, m_ySize, m_zSize);
        	sog.RootPart.Scale = m_size;
        	m_prims.Add(sog); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(sog, false);  //add it to the scene (not backed up to the db)
        }

        void OnChat(Object sender, OSChatMessage chat)
        {
        	if (chat.Channel != m_channel)
            {
                return;
            }
            else if (chat.Message == "show")
            {
                if (m_isHidden == true)
                {
                    Dialog("Show...");
                    InitializePyramid(m_scene);
                    m_isHidden = false;
                }
                else
                {
                    Dialog("Already shown");
                }
            }
            else if (chat.Message == "hide")
            {
                if (m_isHidden == false)
                {
                    Dialog("Hide...");
                    foreach (SceneObjectGroup sog in m_prims)
                    {
                        m_scene.DeleteSceneObject(sog, false);
                    }
                    m_prims.Clear();
                    m_isHidden = true;
                }
                else
                {
                    Dialog("Already hidden");
                }
            }
			else if (chat.Message == "step")
            {
                if (m_isHidden == false)
                {
        		    Dialog("Updating pyramid...");
        		    foreach(SceneObjectGroup sog in m_prims)
                    {
        			    DoSierpinski(sog, m_size);
        		    }
        		    m_size = new Vector3(m_size.X / 2, m_size.Y / 2, m_size.Z /2);
                    m_prims.Clear();
                    Dialog(m_newprims.Count + " prims");
                    m_prims = new List<SceneObjectGroup>(m_newprims);
                    m_newprims.Clear();
                }
                else
                {
                    Dialog("Must 'show' first...");
                }
        	}
            else
            {
                Dialog("Invalid command");
            }
        }

        void DoSierpinski(SceneObjectGroup sog, Vector3 scale)
        {
            //replace the original prim with 5 new prims
        	Vector3 newsize = new Vector3(scale.X / 2, scale.Y / 2, scale.Z / 2);
        	Vector3 offset = new Vector3(scale.X / 4, scale.Y / 4, scale.Z / 4);
        	Vector3 pos = sog.AbsolutePosition;
        	//Move and resize the existing prim to become the new prim#1
            //This is much faster than creating a new one and deleting the old
            Vector3 newpos = new Vector3(pos.X, pos.Y, pos.Z + offset.Z);
            sog.AbsolutePosition = newpos;
            sog.RootPart.Scale = newsize;
            sog.ScheduleGroupForFullUpdate();
            m_newprims.Add(sog);
            // Add new prim#2
            PrimitiveBaseShape prim2 = PrimitiveBaseShape.CreateBox();
            prim2.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
        	newpos = new Vector3(pos.X - offset.X, pos.Y - offset.Y, pos.Z - offset.Z);
        	SceneObjectGroup newsog2 = new SceneObjectGroup(UUID.Zero, newpos, prim2);
        	newsog2.RootPart.Scale = newsize;
        	m_newprims.Add(newsog2); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog2, false);  //add it to the scene (not backed up to the db)
            Primitive.TextureEntry tex = newsog2.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog2.RootPart.UpdateTexture(tex);
        	// Add new prim#3
            PrimitiveBaseShape prim3 = PrimitiveBaseShape.CreateBox();
            prim3.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
        	newpos = new Vector3(pos.X - offset.X, pos.Y + offset.Y, pos.Z - offset.Z);
        	SceneObjectGroup newsog3 = new SceneObjectGroup(UUID.Zero, newpos, prim3);
        	newsog3.RootPart.Scale = newsize;
        	m_newprims.Add(newsog3); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog3, false);  //add it to the scene (not backed up to the db)
            tex = newsog3.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog3.RootPart.UpdateTexture(tex);
			// Add new prim#4
            PrimitiveBaseShape prim4 = PrimitiveBaseShape.CreateBox();
            prim4.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
         	newpos = new Vector3(pos.X + offset.X, pos.Y - offset.Y, pos.Z - offset.Z);
        	SceneObjectGroup newsog4 = new SceneObjectGroup(UUID.Zero, newpos, prim4);
        	newsog4.RootPart.Scale = newsize;
            m_newprims.Add(newsog4); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog4, false);  //add it to the scene (not backed up to the db)
            tex = newsog4.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog4.RootPart.UpdateTexture(tex);
        	// Add new prim#5
            PrimitiveBaseShape prim5 = PrimitiveBaseShape.CreateBox();
            prim5.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
        	newpos = new Vector3(pos.X + offset.X, pos.Y + offset.Y, pos.Z - offset.Z);
        	SceneObjectGroup newsog5 = new SceneObjectGroup(UUID.Zero, newpos, prim5);
        	newsog5.RootPart.Scale = newsize;
        	m_newprims.Add(newsog5); //add it to our list of managed objects
        	m_scene.AddNewSceneObject(newsog5, false);  //add it to the scene (not backed up to the db)
            tex = newsog5.RootPart.Shape.Textures;
            tex.DefaultTexture.RGBA = RandomColor();
            newsog5.RootPart.UpdateTexture(tex);
        }

        Color4 RandomColor()
        {
            //Randomly pick a color - but pick green more often than other colors
            Color4 randomcolor;
            int randomNumber = m_random.Next(0,10);
            if (randomNumber < 7)
            {
                randomcolor = new Color4(0.0f, 0.50f, 0.0f, 1.0f); //Green
            }
            else if (randomNumber == 7)
            {
                randomcolor = new Color4(1.0f, 0.0f, 0.0f, 1.0f); //Red
            }
            else if (randomNumber == 8)
            {
                randomcolor = new Color4(0.0f, 0.0f, 1.0f, 1.0f); //Blue
            }
            else
            {
                randomcolor = new Color4(1.0f, 1.0f, 0.0f, 1.0f); //Yellow
            }
            return randomcolor;
        }

        void Dialog(string message)
        {
            if (m_dialogmod != null)
            {
                m_dialogmod.SendGeneralAlert("Sierpinski Module: " + message);
            }
        }
    }
}
