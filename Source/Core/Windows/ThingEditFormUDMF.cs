﻿
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.GZBuilder.Data;
using CodeImp.DoomBuilder.GZBuilder.Tools;
using CodeImp.DoomBuilder.Types;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// Dialog window that allows viewing and editing of Thing properties.
	/// </summary>
	internal partial class ThingEditFormUDMF : DelayedForm
	{
		#region ================== Events

		public event EventHandler OnValuesChanged; //mxd

		#endregion

		#region ================== Variables

		private ICollection<Thing> things;
		private ThingTypeInfo thinginfo;
		private bool preventchanges;
		private bool preventmapchange; //mxd
		private bool undocreated; //mxd
		private static bool useabsoluteheight; //mxd
		private string arg0str;
		private bool haveArg0Str;
		private List<ThingProperties> thingprops; //mxd
		private readonly string[] renderstyles; //mxd

		//mxd. Window setup stuff
		private static Point location = Point.Empty;
		private static int activetab;

		private struct ThingProperties //mxd
		{
			//public readonly int Type;
			public readonly int AngleDoom;
			public readonly int Pitch;
			public readonly int Roll;
			public readonly float ScaleX;
			public readonly float ScaleY;
			public readonly float X;
			public readonly float Y;
			public readonly float Z;

			public ThingProperties(Thing t) 
			{
				X = t.Position.x;
				Y = t.Position.y;
				Z = t.Position.z;
				//Type = t.Type;
				AngleDoom = t.AngleDoom;
				Pitch = t.Pitch;
				Roll = t.Roll;
				ScaleX = t.ScaleX;
				ScaleY = t.ScaleY;
			}
		}

		#endregion

		#region ================== Constructor

		// Constructor
		public ThingEditFormUDMF() 
		{
			// Initialize
			InitializeComponent();

			//mxd. Widow setup
			if(location != Point.Empty) 
			{
				this.StartPosition = FormStartPosition.Manual;
				this.Location = location;
				if(General.Settings.StoreSelectedEditTab && activetab > 0) tabs.SelectTab(activetab);
			}

			// Fill flags list
			foreach(KeyValuePair<string, string> tf in General.Map.Config.ThingFlags)
				flags.Add(tf.Value, tf.Key);
			flags.Enabled = (General.Map.Config.ThingFlags.Count > 0);

			// Fill actions list
			action.GeneralizedCategories = General.Map.Config.GenActionCategories;
			action.AddInfo(General.Map.Config.SortedLinedefActions.ToArray());

			// Setup renderstyles
			renderstyles = new string[General.Map.Config.ThingRenderStyles.Count];
			General.Map.Config.ThingRenderStyles.Keys.CopyTo(renderstyles, 0);

			// Fill renderstyles
			foreach(KeyValuePair<string, string> lf in General.Map.Config.ThingRenderStyles)
				renderStyle.Items.Add(lf.Value);
			renderStyle.Enabled = (General.Map.Config.ThingRenderStyles.Count > 0);
			labelrenderstyle.Enabled = (General.Map.Config.ThingRenderStyles.Count > 0);

			// Initialize custom fields editor
			fieldslist.Setup("thing");

			// Fill universal fields list
			fieldslist.ListFixedFields(General.Map.Config.ThingFields);

			// Tag/Effects
			scriptNumbers.Location = new Point(arg0.Location.X, arg0.Location.Y + 2);
			scriptNames.Location = scriptNumbers.Location;

			// Setup script names
			foreach(ScriptItem nsi in General.Map.NamedScripts)
				scriptNames.Items.Add(new ColoredComboBoxItem(nsi, nsi.IsInclude ? SystemColors.HotTrack : SystemColors.WindowText));
			foreach(ScriptItem si in General.Map.NumberedScripts)
				scriptNumbers.Items.Add(new ColoredComboBoxItem(si, si.IsInclude ? SystemColors.HotTrack : SystemColors.WindowText));
			scriptNames.DropDownWidth = Tools.GetDropDownWidth(scriptNames);
			scriptNumbers.DropDownWidth = Tools.GetDropDownWidth(scriptNumbers);

			// Thing height?
			posZ.Visible = General.Map.FormatInterface.HasThingHeight;
			zlabel.Visible = General.Map.FormatInterface.HasThingHeight;
			cbAbsoluteHeight.Visible = General.Map.FormatInterface.HasThingHeight; //mxd

			//mxd. Decimals allowed?
			if(General.Map.FormatInterface.VertexDecimals > 0) 
			{
				posX.AllowDecimal = true;
				posY.AllowDecimal = true;
				posZ.AllowDecimal = true;
			}

			// Setup types list
			thingtype.Setup();
		}

		#endregion

		#region ================== Methods

		// This sets up the form to edit the given things
		public void Setup(ICollection<Thing> things) 
		{
			preventchanges = true;

			// Keep this list
			this.things = things;
			if(things.Count > 1) this.Text = "Edit Things (" + things.Count + ")";
			hint.Visible = things.Count > 1; //mxd
			hintlabel.Visible = things.Count > 1; //mxd
			thingtype.UseMultiSelection = things.Count > 1; //mxd

			////////////////////////////////////////////////////////////////////////
			// Set all options to the first thing properties
			////////////////////////////////////////////////////////////////////////

			Thing ft = General.GetByIndex(things, 0);

			// Set type
			thingtype.SelectType(ft.Type);

			// Flags
			foreach (CheckBox c in flags.Checkboxes) 
			{
				if (ft.Flags.ContainsKey(c.Tag.ToString())) c.Checked = ft.Flags[c.Tag.ToString()];
			}

			// Coordination
			angle.Text = ft.AngleDoom.ToString();
			cbAbsoluteHeight.Checked = useabsoluteheight; //mxd

			//mxd
			ft.DetermineSector();
			float floorheight = (ft.Sector != null ? Sector.GetFloorPlane(ft.Sector).GetZ(ft.Position) : 0);
			posX.Text = (ft.Position.x).ToString();
			posY.Text = (ft.Position.y).ToString();
			posZ.Text = (useabsoluteheight ? ((float)Math.Round(ft.Position.z + floorheight, General.Map.FormatInterface.VertexDecimals)).ToString() : (ft.Position.z).ToString());
			posX.ButtonStep = General.Map.Grid.GridSize;
			posY.ButtonStep = General.Map.Grid.GridSize;
			posZ.ButtonStep = General.Map.Grid.GridSize;

			// Custom fields
			fieldslist.SetValues(ft.Fields, true);
			conversationID.Text = ft.Fields.GetValue("conversation", 0).ToString();
			gravity.Text = ft.Fields.GetValue("gravity", 1.0f).ToString();
			score.Text = ft.Fields.GetValue("score", 0).ToString();
			health.Text = ft.Fields.GetValue("health", 1).ToString();
			alpha.Text = ft.Fields.GetValue("alpha", 1.0f).ToString();
			color.SetValueFrom(ft.Fields);
			arg0str = ft.Fields.GetValue("arg0str", string.Empty);
			haveArg0Str = !string.IsNullOrEmpty(arg0str);
			scale.SetValues(ft.ScaleX, ft.ScaleY, true);
			pitch.Text = ft.Pitch.ToString();
			roll.Text = ft.Roll.ToString();
			renderStyle.SelectedIndex = Array.IndexOf(renderstyles, ft.Fields.GetValue("renderstyle", "normal"));

			// Action/tags
			action.Value = ft.Action;
			tagSelector.Setup(UniversalType.ThingTag);
			tagSelector.SetTag(ft.Tag);
			arg0.SetValue(ft.Args[0]);
			arg1.SetValue(ft.Args[1]);
			arg2.SetValue(ft.Args[2]);
			arg3.SetValue(ft.Args[3]);
			arg4.SetValue(ft.Args[4]);

			////////////////////////////////////////////////////////////////////////
			// Now go for all lines and change the options when a setting is different
			////////////////////////////////////////////////////////////////////////

			thingprops = new List<ThingProperties>();

			// Go for all things
			foreach(Thing t in things) 
			{
				//mxd. Update sector info
				t.DetermineSector();

				// Type does not match?
				ThingTypeInfo info = thingtype.GetSelectedInfo(); //mxd
				if(info != null && info.Index != t.Type) thingtype.ClearSelectedType();

				// Flags
				foreach(CheckBox c in flags.Checkboxes) 
				{
					if(c.CheckState == CheckState.Indeterminate) continue; //mxd
					if(t.IsFlagSet(c.Tag.ToString()) != c.Checked) 
					{
						c.ThreeState = true;
						c.CheckState = CheckState.Indeterminate;
					}
				}

				// Coordination
				if(t.AngleDoom.ToString() != angle.Text) angle.Text = "";

				//mxd. Position
				if((t.Position.x).ToString() != posX.Text) posX.Text = "";
				if((t.Position.y).ToString() != posY.Text) posY.Text = "";
				if(useabsoluteheight && t.Sector != null) 
				{
					if(((float)Math.Round(Sector.GetFloorPlane(t.Sector).GetZ(t.Position) + t.Position.z, General.Map.FormatInterface.VertexDecimals)).ToString() != posZ.Text)
						posZ.Text = "";
				} 
				else if((t.Position.z).ToString() != posZ.Text) 
				{
					posZ.Text = "";
				}

				// Action/tags
				if(t.Action != action.Value) action.Empty = true;
				if(t.Tag != ft.Tag) tagSelector.ClearTag(); //mxd
				if(t.Args[0] != arg0.GetResult(-1)) arg0.ClearValue();
				if(t.Args[1] != arg1.GetResult(-1)) arg1.ClearValue();
				if(t.Args[2] != arg2.GetResult(-1)) arg2.ClearValue();
				if(t.Args[3] != arg3.GetResult(-1)) arg3.ClearValue();
				if(t.Args[4] != arg4.GetResult(-1)) arg4.ClearValue();

				//mxd. Custom fields
				fieldslist.SetValues(t.Fields, false);
				if (t.Fields.GetValue("conversation", 0).ToString() != conversationID.Text) conversationID.Text = "";
				if (t.Fields.GetValue("gravity", 1.0f).ToString() != gravity.Text) gravity.Text = "";
				if (t.Fields.GetValue("score", 0).ToString() != score.Text) score.Text = "";
				if (t.Fields.GetValue("health", 1).ToString() != health.Text) health.Text = "";
				if (t.Fields.GetValue("alpha", 1.0f).ToString() != alpha.Text) alpha.Text = "";

				scale.SetValues(t.ScaleX, t.ScaleY, false);
				color.SetValueFrom(t.Fields);

				if (t.Pitch.ToString() != pitch.Text) pitch.Text = "";
				if (t.Roll.ToString() != roll.Text) roll.Text = "";

				//Render style
				if(renderStyle.SelectedIndex > -1 && renderStyle.SelectedIndex != Array.IndexOf(renderstyles, t.Fields.GetValue("renderstyle", "normal")))
					renderStyle.SelectedIndex = -1;

				if (arg0str != t.Fields.GetValue("arg0str", string.Empty)) 
				{
					haveArg0Str = true;
					arg0str = string.Empty;
				}

				//mxd. Store initial properties
				thingprops.Add(new ThingProperties(t));

				//mxd. add user vars
				/*if(info != null && info.Actor != null && info.Actor.UserVars.Count > 0) 
				 {
					foreach(string s in info.Actor.UserVars) 
					{
						if(!t.Fields.ContainsKey(s))
							fieldslist.SetValue(s, 0, CodeImp.DoomBuilder.Types.UniversalType.Integer);
					}
				}*/
			}

			preventchanges = false;

			//mxd. Trigger updates manually...
			preventmapchange = true;
			angle_WhenTextChanged(angle, EventArgs.Empty);
			pitch_WhenTextChanged(pitch, EventArgs.Empty);
			roll_WhenTextChanged(roll, EventArgs.Empty);
			flags_OnValueChanged(flags, EventArgs.Empty);
			preventmapchange = false;

			UpdateScriptControls(); //mxd
			actionhelp.UpdateAction(action.GetValue()); //mxd
			labelScale.Enabled = scale.NonDefaultValue; //mxd

			//mxd. Set intial script-related values, if required
			if(Array.IndexOf(GZBuilder.GZGeneral.ACS_SPECIALS, action.Value) != -1) 
			{
				if(haveArg0Str) 
				{
					scriptNames.Text = arg0str;
					arg0label.Text = "Script Name:";
				} 
				else 
				{
					int a0 = arg0.GetResult(0);
					if(a0 > 0) 
					{
						for(int i = 0; i < General.Map.NumberedScripts.Count; i++) 
						{
							if(General.Map.NumberedScripts[i].Index == a0) 
							{
								scriptNumbers.SelectedIndex = i;
								break;
							}
						}
					} 

					if(scriptNumbers.SelectedIndex == -1) 
					{
						scriptNumbers.Text = a0.ToString();
					}
				}
			} 
			else 
			{
				scriptNumbers.Text = "0";
			}
		}

		//mxd
		private void MakeUndo() 
		{
			if(undocreated) return;
			undocreated = true;

			//mxd. Make undo
			General.Map.UndoRedo.CreateUndo("Edit " + (things.Count > 1 ? things.Count + " things" : "thing"));
			foreach(Thing t in things) t.Fields.BeforeFieldsChange();
		}

		//mxd
		private void UpdateScriptControls() 
		{
			if(Array.IndexOf(GZBuilder.GZGeneral.ACS_SPECIALS, action.Value) != -1) 
			{
				bool showNamedScripts = haveArg0Str;
				cbArgStr.Visible = true;
				cbArgStr.Checked = showNamedScripts;
				scriptNames.Visible = showNamedScripts;
				scriptNumbers.Visible = !showNamedScripts;
			} 
			else 
			{
				cbArgStr.Visible = false;
				scriptNames.Visible = false;
				scriptNumbers.Visible = false;
				cbArgStr.Checked = false;
			}

			arg0.Visible = (!scriptNames.Visible && !scriptNumbers.Visible);
		}

		//mxd
		private void UpdateArgument(ArgumentBox arg, Label label, ArgumentInfo info) 
		{
			label.Text = info.Title + ":";
			label.Enabled = info.Used;
			arg.ForeColor = (label.Enabled ? SystemColors.WindowText : SystemColors.GrayText);
			arg.Setup(info);

			// Update tooltip
			if(info.Used && !string.IsNullOrEmpty(info.ToolTip)) 
			{
				tooltip.SetToolTip(label, info.ToolTip);
				label.Font = new Font(label.Font, FontStyle.Underline);
				label.ForeColor = SystemColors.HotTrack;
			} 
			else 
			{
				tooltip.SetToolTip(label, null);
				label.Font = new Font(label.Font, FontStyle.Regular);
				label.ForeColor = SystemColors.WindowText;
			}
		}

		#endregion

		#region ================== Events

		//mxd
		private void thingtype_OnTypeDoubleClicked() 
		{
			apply_Click(this, EventArgs.Empty);
		}

		// Action changes
		private void action_ValueChanges(object sender, EventArgs e) 
		{
			int showaction = 0;
			ArgumentInfo[] arginfo;

			// Only when line type is known, otherwise use the thing arguments
			if(General.Map.Config.LinedefActions.ContainsKey(action.Value)) showaction = action.Value;
			if((showaction == 0) && (thinginfo != null)) arginfo = thinginfo.Args;
			else arginfo = General.Map.Config.LinedefActions[showaction].Args;

			// Change the argument descriptions
			UpdateArgument(arg0, arg0label, arginfo[0]); //mxd
			UpdateArgument(arg1, arg1label, arginfo[1]); //mxd
			UpdateArgument(arg2, arg2label, arginfo[2]); //mxd
			UpdateArgument(arg3, arg3label, arginfo[3]); //mxd
			UpdateArgument(arg4, arg4label, arginfo[4]); //mxd

			if(!preventchanges) 
			{
				MakeUndo(); //mxd
				
				// mxd. Apply action's or thing's default arguments
				if(showaction != 0 || thinginfo != null) 
				{
					arg0.SetDefaultValue();
					arg1.SetDefaultValue();
					arg2.SetDefaultValue();
					arg3.SetDefaultValue();
					arg4.SetDefaultValue();
				} 
				else //or set them to 0
				{
					arg0.SetValue(0);
					arg1.SetValue(0);
					arg2.SetValue(0);
					arg3.SetValue(0);
					arg4.SetValue(0);
				}

				//mxd. Update what must be updated
				UpdateScriptControls();
				actionhelp.UpdateAction(showaction);
			}
		}

		// Browse Action clicked
		private void browseaction_Click(object sender, EventArgs e) 
		{
			action.Value = ActionBrowserForm.BrowseAction(this, action.Value);
		}

		// Angle text changes
		private void angle_WhenTextChanged(object sender, EventArgs e) 
		{
			if (preventchanges) return;
			preventchanges = true;
			anglecontrol.Angle = angle.GetResult(GZBuilder.Controls.AngleControl.NO_ANGLE);
			preventchanges = false;
			if(!preventmapchange) ApplyAngleChange(); //mxd
		}

		//mxd. Angle control clicked
		private void anglecontrol_AngleChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			angle.Text = anglecontrol.Angle.ToString();
			if(!preventmapchange) ApplyAngleChange();
		}

		private void pitch_WhenTextChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			int p = pitch.GetResult(GZBuilder.Controls.AngleControl.NO_ANGLE);
			preventchanges = true;
			pitchControl.Angle = (p == GZBuilder.Controls.AngleControl.NO_ANGLE ? p : p + 90);
			preventchanges = false;
			if(!preventmapchange) ApplyPitchChange();
		}

		private void pitchControl_AngleChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			pitch.Text = (General.ClampAngle(pitchControl.Angle - 90)).ToString();
			if(!preventmapchange) ApplyPitchChange();
		}

		private void roll_WhenTextChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			int r = roll.GetResult(GZBuilder.Controls.AngleControl.NO_ANGLE);
			preventchanges = true;
			rollControl.Angle = (r == GZBuilder.Controls.AngleControl.NO_ANGLE ? r : r + 90);
			preventchanges = false;
			if(!preventmapchange) ApplyRollChange();
		}

		private void rollControl_AngleChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			roll.Text = (General.ClampAngle(rollControl.Angle - 90)).ToString();
			if(!preventmapchange) ApplyRollChange();
		}

		// Apply clicked
		private void apply_Click(object sender, EventArgs e) 
		{
			// Make Undo
			MakeUndo(); //mxd
			
			List<string> defaultflags = new List<string>();

			// Verify the tag
			if(General.Map.FormatInterface.HasThingTag) //mxd
			{
				tagSelector.ValidateTag();//mxd
				if(((tagSelector.GetTag(0) < General.Map.FormatInterface.MinTag) || (tagSelector.GetTag(0) > General.Map.FormatInterface.MaxTag))) 
				{
					General.ShowWarningMessage("Thing tag must be between " + General.Map.FormatInterface.MinTag + " and " + General.Map.FormatInterface.MaxTag + ".", MessageBoxButtons.OK);
					return;
				}
			}

			// Verify the type
			if(((thingtype.GetResult(0) < General.Map.FormatInterface.MinThingType) || (thingtype.GetResult(0) > General.Map.FormatInterface.MaxThingType))) 
			{
				General.ShowWarningMessage("Thing type must be between " + General.Map.FormatInterface.MinThingType + " and " + General.Map.FormatInterface.MaxThingType + ".", MessageBoxButtons.OK);
				return;
			}

			// Verify the action
			if(General.Map.FormatInterface.HasThingAction && ((action.Value < General.Map.FormatInterface.MinAction) || (action.Value > General.Map.FormatInterface.MaxAction))) 
			{
				General.ShowWarningMessage("Thing action must be between " + General.Map.FormatInterface.MinAction + " and " + General.Map.FormatInterface.MaxAction + ".", MessageBoxButtons.OK);
				return;
			}

			bool hasAcs = !action.Empty && Array.IndexOf(GZBuilder.GZGeneral.ACS_SPECIALS, action.Value) != -1; //mxd

			//mxd
			string[] rskeys = null;
			if(General.Map.Config.ThingRenderStyles.Count > 0) 
			{
				rskeys = new string[General.Map.Config.ThingRenderStyles.Count];
				General.Map.Config.ThingRenderStyles.Keys.CopyTo(rskeys, 0);
			}

			// Go for all the things
			int tagoffset = 0; //mxd
			foreach(Thing t in things) 
			{
				// Coordination
				//mxd. Randomize rotations?
				if(cbrandomangle.Checked) t.Rotate(General.Random(0, 359));
				if(cbrandompitch.Checked) t.SetPitch(General.Random(0, 359));
				if(cbrandomroll.Checked) t.SetRoll(General.Random(0, 359));

				//mxd. Check position
				float px = General.Clamp(t.Position.x, General.Map.Config.LeftBoundary, General.Map.Config.RightBoundary);
				float py = General.Clamp(t.Position.y, General.Map.Config.BottomBoundary, General.Map.Config.TopBoundary);
				if(t.Position.x != px || t.Position.y != py) t.Move(new Vector2D(px, py));

				// Apply all flags
				foreach(CheckBox c in flags.Checkboxes) 
				{
					if(c.CheckState == CheckState.Checked)
						t.SetFlag(c.Tag.ToString(), true);
					else if(c.CheckState == CheckState.Unchecked)
						t.SetFlag(c.Tag.ToString(), false);
				}

				// Action/tags
				t.Tag = General.Clamp(tagSelector.GetSmartTag(t.Tag, tagoffset++), General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag); //mxd
				if(!action.Empty) 
				{
					t.Action = action.Value;

					//mxd. Script name/number handling
					if(hasAcs) 
					{
						if(!cbArgStr.Checked) //apply script number
						{ 
							if(!string.IsNullOrEmpty(scriptNumbers.Text)) 
							{
								if(scriptNumbers.SelectedItem != null)
									t.Args[0] = ((ScriptItem)((ColoredComboBoxItem)scriptNumbers.SelectedItem).Value).Index;
								else if(!int.TryParse(scriptNumbers.Text.Trim(), out t.Args[0]))
									t.Args[0] = 0;

								if(t.Fields.ContainsKey("arg0str")) t.Fields.Remove("arg0str");
							}
						} 
						else //apply arg0str
						{ 
							if(!string.IsNullOrEmpty(scriptNames.Text))
								t.Fields["arg0str"] = new UniValue(UniversalType.String, scriptNames.Text);
						}
					} 
					else 
					{
						t.Args[0] = arg0.GetResult(t.Args[0]);
						if(t.Fields.ContainsKey("arg0str")) t.Fields.Remove("arg0str");
					}
				} 
				else 
				{
					t.Args[0] = arg0.GetResult(t.Args[0]);
				}

				t.Args[1] = arg1.GetResult(t.Args[1]);
				t.Args[2] = arg2.GetResult(t.Args[2]);
				t.Args[3] = arg3.GetResult(t.Args[3]);
				t.Args[4] = arg4.GetResult(t.Args[4]);

				//mxd. Custom fields
				fieldslist.Apply(t.Fields);
				if (!string.IsNullOrEmpty(conversationID.Text))
					UDMFTools.SetInteger(t.Fields, "conversation", conversationID.GetResult(t.Fields.GetValue("conversation", 0)), 0);
				if (!string.IsNullOrEmpty(gravity.Text))
					UDMFTools.SetFloat(t.Fields, "gravity", gravity.GetResultFloat(t.Fields.GetValue("gravity", 1.0f)), 1.0f);
				if (!string.IsNullOrEmpty(health.Text))
					UDMFTools.SetInteger(t.Fields, "health", health.GetResult(t.Fields.GetValue("health", 1)), 1);
				if (!string.IsNullOrEmpty(score.Text))
					UDMFTools.SetInteger(t.Fields, "score", score.GetResult(t.Fields.GetValue("score", 0)), 0);
				if (!string.IsNullOrEmpty(alpha.Text))
					UDMFTools.SetFloat(t.Fields, "alpha", alpha.GetResultFloat(t.Fields.GetValue("alpha", 1.0f)), 1.0f);
				if (rskeys != null && renderStyle.SelectedIndex > -1)
					UDMFTools.SetString(t.Fields, "renderstyle", rskeys[renderStyle.SelectedIndex], "normal");

				color.ApplyTo(t.Fields, t.Fields.GetValue("fillcolor", 0));

				// Update settings
				t.UpdateConfiguration();
			}

			// Set as defaults
			foreach (CheckBox c in flags.Checkboxes) 
			{
				if (c.CheckState == CheckState.Checked) defaultflags.Add(c.Tag.ToString());
			}
			General.Settings.DefaultThingType = thingtype.GetResult(General.Settings.DefaultThingType);
			General.Settings.DefaultThingAngle = Angle2D.DegToRad((float)angle.GetResult((int)Angle2D.RadToDeg(General.Settings.DefaultThingAngle) - 90) + 90);
			General.Settings.SetDefaultThingFlags(defaultflags);

			// Done
			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty); //mxd
			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		// Cancel clicked
		private void cancel_Click(object sender, EventArgs e) 
		{
			//mxd. Perform undo?
			if(undocreated) General.Map.UndoRedo.WithdrawUndo();

			// Be gone
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		//mxd
		private void cbArgStr_CheckedChanged(object sender, EventArgs e) 
		{
			if(!cbArgStr.Visible) return;
			scriptNames.Visible = cbArgStr.Checked;
			scriptNumbers.Visible = !cbArgStr.Checked;
			arg0label.Text = cbArgStr.Checked ? "Script Name:" : "Script Number:";
		}

		//mxd
		private void cbAbsoluteHeight_CheckedChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			MakeUndo();

			useabsoluteheight = cbAbsoluteHeight.Checked;

			preventchanges = true;

			//update label text
			Thing ft = General.GetByIndex(things, 0);
			float z = ft.Position.z;
			if(useabsoluteheight && ft.Sector != null) z += Sector.GetFloorPlane(ft.Sector).GetZ(ft.Position);
			posZ.Text = ((float)Math.Round(z, General.Map.FormatInterface.VertexDecimals)).ToString();

			foreach(Thing t in things) 
			{
				z = t.Position.z;
				if(useabsoluteheight && t.Sector != null) z += Sector.GetFloorPlane(t.Sector).GetZ(t.Position);
				string ztext = ((float)Math.Round(z, General.Map.FormatInterface.VertexDecimals)).ToString();
				if(posZ.Text != ztext) 
				{
					posZ.Text = "";
					break;
				}
			}

			preventchanges = false;
		}

		//mxd
		private void tabcustom_MouseEnter(object sender, EventArgs e) 
		{
			fieldslist.Focus();
		}

		//mxd
		private void ThingEditForm_FormClosing(object sender, FormClosingEventArgs e) 
		{
			location = this.Location;
			activetab = tabs.SelectedIndex;
		}

		// Help
		private void ThingEditForm_HelpRequested(object sender, HelpEventArgs hlpevent) 
		{
			General.ShowHelp("w_thingeditor.html");
			hlpevent.Handled = true;
		}

		#endregion

		#region ================== mxd. Realtime events

		private void posX_WhenTextChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			// Update values
			foreach(Thing t in things)
				t.Move(new Vector2D(posX.GetResultFloat(thingprops[i++].X), t.Position.y));

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		private void posY_WhenTextChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			// Update values
			foreach(Thing t in things)
				t.Move(new Vector2D(t.Position.x, posY.GetResultFloat(thingprops[i++].Y)));

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		private void posZ_WhenTextChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			if(string.IsNullOrEmpty(posZ.Text)) 
			{
				// Restore values
				foreach(Thing t in things)
					t.Move(new Vector3D(t.Position.x, t.Position.y, thingprops[i++].Z));
			} 
			else 
			{
				// Update values
				foreach(Thing t in things) 
				{
					float z = posZ.GetResultFloat(thingprops[i++].Z);
					if(useabsoluteheight && !posZ.CheckIsRelative() && t.Sector != null)
						z -= (float)Math.Round(Sector.GetFloorPlane(t.Sector).GetZ(t.Position.x, t.Position.y), General.Map.FormatInterface.VertexDecimals);
					t.Move(new Vector3D(t.Position.x, t.Position.y, z));
				}
			}

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		private void scale_OnValuesChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			foreach (Thing t in things)
			{
				float sx = scale.GetValue1(thingprops[i].ScaleX);
				float sy = scale.GetValue2(thingprops[i].ScaleY);
				t.SetScale((sx == 0 ? 1.0f : sx), (sy == 0 ? 1.0f : sy));
				i++;
			}

			General.Map.IsChanged = true;
			labelScale.Enabled = scale.NonDefaultValue;
			if (OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		// Selected type changes
		private void thingtype_OnTypeChanged(ThingTypeInfo value) 
		{
			thinginfo = value;

			// Update arguments
			action_ValueChanges(this, EventArgs.Empty);

			//mxd. Update things
			if (preventchanges 
					|| (thingtype.GetResult(0) < General.Map.FormatInterface.MinThingType) 
					|| (thingtype.GetResult(0) > General.Map.FormatInterface.MaxThingType))
				return;

			MakeUndo(); //mxd

			foreach(Thing t in things) 
			{
				//Set type
				t.Type = thingtype.GetResult(t.Type);

				// Update settings
				t.UpdateConfiguration();
			}

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		//mxd
		private void ApplyAngleChange() 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			//restore values
			if(string.IsNullOrEmpty(angle.Text)) 
			{
				foreach(Thing t in things) t.Rotate(thingprops[i++].AngleDoom);
			} 
			else //update values
			{
				foreach(Thing t in things)
					t.Rotate(angle.GetResult(thingprops[i++].AngleDoom));
			}

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		private void ApplyPitchChange() 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			//restore values
			if (string.IsNullOrEmpty(pitch.Text)) 
			{
				foreach (Thing t in things) t.SetPitch(thingprops[i++].Pitch);
			} 
			else //update values
			{ 
				foreach (Thing t in things)
					t.SetPitch(pitch.GetResult(thingprops[i++].Pitch));
			}

			General.Map.IsChanged = true;
			if(OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		//mxd
		private void ApplyRollChange() 
		{
			if(preventchanges) return;
			MakeUndo(); //mxd
			int i = 0;

			//restore values
			if (string.IsNullOrEmpty(roll.Text)) 
			{
				foreach (Thing t in things) t.SetRoll(thingprops[i++].Roll);
			} 
			else //update values
			{ 
				foreach (Thing t in things)
					t.SetRoll(roll.GetResult(thingprops[i++].Roll));
			}

			General.Map.IsChanged = true;
			if (OnValuesChanged != null) OnValuesChanged(this, EventArgs.Empty);
		}

		private void flags_OnValueChanged(object sender, EventArgs e) 
		{
			if(preventchanges) return;

			string warn = ThingFlagsCompare.CheckThingEditFormFlags(flags.Checkboxes);
			if(!string.IsNullOrEmpty(warn)) 
			{
				//got missing flags
				tooltip.SetToolTip(missingflags, warn);
				missingflags.Visible = true;
				settingsgroup.ForeColor = Color.DarkRed;
				return;
			}

			//everything is OK
			missingflags.Visible = false;
			settingsgroup.ForeColor = SystemColors.ControlText;
		}

		private void cbrandomangle_CheckedChanged(object sender, EventArgs e)
		{
			angle.Enabled = !cbrandomangle.Checked;
			groupangle.Enabled = !cbrandomangle.Checked;
		}

		private void cbrandompitch_CheckedChanged(object sender, EventArgs e)
		{
			pitch.Enabled = !cbrandompitch.Checked;
			grouppitch.Enabled = !cbrandompitch.Checked;
		}

		private void cbrandomroll_CheckedChanged(object sender, EventArgs e)
		{
			roll.Enabled = !cbrandomroll.Checked;
			grouproll.Enabled = !cbrandomroll.Checked;
		}

		#endregion

	}
}