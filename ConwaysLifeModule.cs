/*
 * Copyright (c) Contributors http://github.com/aduffy70/ConwaysGOL
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
using System.Timers;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace ConwaysLifeModule {
    public class ConwaysLifeModule : IRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		List<SceneObjectGroup> prims = new List<SceneObjectGroup>();
		float xcenter = 65f; //inworld x coordinate for the 0,0,0 position
		float ycenter = 65f;  //inworld y coordinate for the 0,0,0 position
		float zcenter = 39f; //inworld z coordinate for the 0,0,0 position
		float aradius = 20f; //overall torus radius
		float bradius = 15f; //torus tube radius
		int xcells = 35;
		int ycells = 35;
		int[] cloudmatrix = new int[36 * 36];
		int[] newvalues = new int[36 * 36];
        Color4 deadcolor = new Color4(0f, 0f, 0f, 0.25f); //color for dead cells
        Color4 livecolor = new Color4(1.0f, 1f, 1f, 1.0f); //color for live cells
        int running = 0; //Keep track of whether the game is running
        int needsupdate = 0; //Only schedule updates if there was a change
		Timer mytimer = new Timer(); //Timer to replace the region heartbeat
		private Scene m_scene;
        
        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config) {
            m_log.Info("[ConwaysLifeModule] Initializing...");
            m_scene = scene;
        }

        public void PostInitialise() {
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
			
			DoLifeModule(m_scene);
        }

        public void Close(){
        }

        public string Name{
            get { return "ConwaysLifeModule"; }
        }

        public bool IsSharedModule {
            get { return false; }
        }

        #endregion

        void DoLifeModule(Scene scene) {
            // We're going to place a torus of dead (white) cells in world
			float twopi = 2f * (float)Math.PI;
			float uspacing = twopi / 36;
			float vspacing = twopi / 36;
			float uradians = 0;
			float vradians = 0;
			int counter = 0;
			for (int y=0; y<=ycells; y++) {
				for (int x=0; x<=xcells; x++) {
					float xpos = xcenter + ((aradius + (bradius * (float)Math.Cos(vradians))) * (float)Math.Cos(uradians));
					float ypos = ycenter + ((aradius + (bradius * (float)Math.Cos(vradians))) * (float)Math.Sin(uradians));
					float zpos = zcenter + (bradius * (float)Math.Sin(vradians));
					Vector3 pos = new Vector3(xpos, ypos, zpos);
					PrimitiveBaseShape prim = PrimitiveBaseShape.CreateSphere();
					prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f"));
					SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
					float size = 0.75f + (Math.Abs((xcells / 2f) - (float)x) / (xcells / 3f));
					sog.RootPart.Scale = new Vector3(size, size, size);
					Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
					cloudmatrix[counter] = 0;
					tex.DefaultTexture.RGBA = deadcolor;
					sog.RootPart.UpdateTexture(tex);	
					prims.Add(sog);
					vradians = vradians + vspacing;
					counter++;
				}
				uradians = uradians + uspacing;
			}
			//Add these objects to the list of managed objects
			//Place the objects visibly on the scene
			running = 0;
			foreach (SceneObjectGroup sogr in prims)
				scene.AddNewSceneObject(sogr, false);
		}
           	
        void OnChat(Object sender, OSChatMessage chat) {
            if ((chat.Channel != 9) || (chat.Message.Length < 5))
				return;
			else if (chat.Message.Substring(0,5) == "reset") { //Stop ticking and set all cells dead
				m_log.Info("[ConwaysLife] resetting...");
				mytimer.Stop();
				running = 0;
				for (int countprims = 0; countprims < (36 * 36); countprims ++) {
					Primitive.TextureEntry tex = prims[countprims].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == livecolor) {
						tex.DefaultTexture.RGBA = deadcolor;
						prims[countprims].RootPart.UpdateTexture(tex);
					}
				}
			}
			else if ((chat.Message.Substring(0,5) == "start") && (running == 0)) { //see which cells are alive and start ticking
				m_log.Info("[ConwaysLife] Starting...");
				for (int counter=0; counter< (36 * 36); counter++){
					Primitive.TextureEntry tex = prims[counter].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == livecolor) {
						cloudmatrix[counter] = 1;
					}
					else {
						cloudmatrix[counter] = 0;
					}
					counter++;
				}		
				running = 1;
				mytimer.Elapsed += new ElapsedEventHandler(TimerEvent);
				mytimer.Interval = 2500;
				mytimer.Start();
			} 
			else if ((chat.Message.Substring(0,4) == "stop") && (running == 1)) { //stop ticking
				m_log.Info("[ConwaysLife] Stopping...");
				running = 0;
				mytimer.Stop();
			}
			else if ((chat.Message.Length > 6) && (running == 0)) { //load an example pattern
				m_log.Info("[ConwaysLife] Setting starting pattern...");
				for (int countprims = 0; countprims < (36 * 36); countprims ++) {
					Primitive.TextureEntry tex = prims[countprims].RootPart.Shape.Textures;
					if (tex.DefaultTexture.RGBA == livecolor) {
						tex.DefaultTexture.RGBA = deadcolor;
						prims[countprims].RootPart.UpdateTexture(tex);
					}
				}
				//Glider
				SetLive(17, 26);
				SetLive(18, 26);
				SetLive(19, 26);
				SetLive(19, 27);
				SetLive(18, 28);
				
				//Glider
				SetLive(26, 17);
				SetLive(27, 17);
				SetLive(26, 18);
				SetLive(28, 18);
				SetLive(26, 19);
				
				//Glider
				SetLive(18, 8); 
				SetLive(17, 9);
				SetLive(17, 10);
				SetLive(18, 10);
				SetLive(19, 10);

				//Glider
				SetLive(10, 17); 
				SetLive(8, 18);
				SetLive(10, 18);
				SetLive(9, 19);
				SetLive(10, 19);

				//Cross
				SetLive(0, 0);
				SetLive(1, 0);
				SetLive(35, 0);
				
				//Cross
				SetLive(18, 17);
				SetLive(18, 18);
				SetLive(18, 19);
				
				//Cross
				SetLive(0, 17);
				SetLive(0, 18);
				SetLive(0, 19);
				
				//Cross
				SetLive(17, 0);
				SetLive(18, 0);
				SetLive(19, 0);
				
			//	SetLive(10, 10);
			//	SetLive(12, 10);
			//	SetLive(14, 10);
			//	SetLive(11, 11);
			//	SetLive(12, 11);
			//	SetLive(14, 11);
			//	SetLive(13, 12);
			//	SetLive(14, 12);
			//	SetLive(10, 13);
			//	SetLive(10, 14);
			//	SetLive(11, 14);
			//	SetLive(12, 14);
			//	SetLive(14, 14);
			}
		}

		void SetLive(int xpos, int ypos) {
			Primitive.TextureEntry tex = prims[ypos * 36 + xpos].RootPart.Shape.Textures;
			cloudmatrix[ypos * 36 + xpos] = 1;
			tex.DefaultTexture.RGBA = livecolor;
			prims[ypos * 36 + xpos].RootPart.UpdateTexture(tex);
		}
		
		void TimerEvent(object source, ElapsedEventArgs e) {
			OnTick();
		}

		void OnTick() {
			Color4 setcolor = new Color4();
    	    int rowabove = new int();
        	int rowbelow = new int();
           	int colleft = new int();
            int colright = new int();
            int counter = 0;
            for (int y=0; y<=ycells; y++) {
				if (y == 0) {
					rowabove = y + 1;
					rowbelow = ycells;
				}
				else if (y == ycells) {
					rowabove = 0;
					rowbelow = y - 1;
				}
				else {
					rowabove = y + 1;
					rowbelow = y - 1;
				}       
            	for (int x=0; x<=xcells; x++) {
					if (x == 0) {
						colright = x + 1;
						colleft = xcells;
					}
					else if (x == xcells) {
						colright = 0;
						colleft = x - 1;
					}
					else {
						colright = x + 1;
						colleft = x - 1;
					} 
                   	if (cloudmatrix[counter] == 0) {
                       	if ((cloudmatrix[rowbelow * 36 + colleft] + cloudmatrix[y * 36 + colleft] + cloudmatrix[rowabove * 36 + colleft] + cloudmatrix[rowbelow * 36 + x] + cloudmatrix[rowabove * 36 + x] + cloudmatrix[rowbelow * 36 + colright] + cloudmatrix[y * 36 + colright] + cloudmatrix[rowabove * 36 + colright]) == 3) {
                       		newvalues[counter] = 1;
                       		setcolor = livecolor;
							needsupdate = 1;
                       	}
                       	else {
                       		newvalues[counter] = 0;
							needsupdate = 0;
                       	}
                   	}
                   	else if (cloudmatrix[counter] == 1) { 
                       	if (((cloudmatrix[rowbelow * 36 + colleft] + cloudmatrix[y * 36 + colleft] + cloudmatrix[rowabove * 36 + colleft] + cloudmatrix[rowbelow * 36 + x] + cloudmatrix[rowabove * 36 + x] + cloudmatrix[rowbelow * 36 + colright] + cloudmatrix[y * 36 + colright] + cloudmatrix[rowabove * 36 + colright]) == 2) || ((cloudmatrix[rowbelow * 36 + colleft] + cloudmatrix[y * 36 + colleft] + cloudmatrix[rowabove * 36 + colleft] + cloudmatrix[rowbelow * 36 + x] + cloudmatrix[rowabove * 36 + x] + cloudmatrix[rowbelow * 36 + colright] + cloudmatrix[y * 36 + colright] + cloudmatrix[rowabove * 36 + colright]) == 3)) {
                       		newvalues[counter] = 1;
							needsupdate = 0;
                       	}
                       	else {
                       		newvalues[counter] = 0;
                       		setcolor = deadcolor;
							needsupdate = 1;
                       	}
                   	}
                   	else {
                       	m_log.Info("Cloudmatrix value is totally out of range!");
                   	}                
					if (needsupdate == 1) {
						Primitive.TextureEntry tex = prims[counter].RootPart.Shape.Textures;
						tex.DefaultTexture.RGBA = setcolor;
                   		prims[counter].RootPart.UpdateTexture(tex);
               			prims[counter].ScheduleGroupForTerseUpdate();
					}
					counter++;
				}
			}
	        Array.Copy(newvalues, cloudmatrix, 36 * 36);
        }
    }
}
