using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;

namespace Xytabich.Tools
{
	[CustomEditor(typeof(BezierBones))]
	public class BezierBonesEditor : Editor
	{
		private SerializedProperty affectCountProp;
		private SerializedProperty enableAffectProp;
		private SerializedProperty targetProp;
		private SerializedProperty controllersProp;
		private ReorderableList controllersList;

		private Transform[] bones = null;
		private int affectBonesCount;

		private Vector3[] calcPointsCache = null;
		private Vector3[] calcResultCache = null;
		private Vector3[] controlLinesCache = null;
		private Vector3[] bezierLineCache = new Vector3[100];
		private int positionControl;
		private BezierBonesTool tool = null;

		void OnEnable()
		{
			affectCountProp = serializedObject.FindProperty("affectBonesCount");
			enableAffectProp = serializedObject.FindProperty("enableBonesAffect");
			targetProp = serializedObject.FindProperty("target");
			controllersProp = serializedObject.FindProperty("controllers");
			controllersList = new ReorderableList(serializedObject, controllersProp);
			controllersList.drawElementCallback = DrawControllerElement;
			controllersList.drawHeaderCallback = DrawControllersHeader;
			controllersList.onRemoveCallback = RemoveControllerElement;
			controllersList.onAddCallback = AddControllerElement;

			affectBonesCount = CountBones(affectCountProp.intValue);
		}

		void OnDisable()
		{
			if(tool != null)
			{
				if(EditorTools.IsActiveTool(tool))
				{
					EditorTools.RestorePreviousPersistentTool();
				}
				DestroyImmediate(tool);
				tool = null;
			}
		}

		void OnSceneGUI()
		{
			serializedObject.Update();
			var bezier = target as BezierBones;

			ReinitIfNeeded();

			var oldColor = Handles.color;
			if(Event.current.type == EventType.Repaint)
			{
				UpdatePointsCache();
				float step = 1f / (bezierLineCache.Length - 1);
				for(var i = 0; i < bezierLineCache.Length; i++)
				{
					bezierLineCache[i] = GetPosition(step * i);
				}
				Handles.color = Color.white;
				Handles.DrawAAPolyLine(bezierLineCache);

				var matrix = Handles.matrix;
				var current = bezier.transform;
				Handles.color = Color.green;
				for(int i = 0; i < affectCountProp.intValue && current != null && current.parent != null; i++)
				{
					var scale = Vector3.Distance(current.position, current.parent.position) * 0.1f;
					Handles.matrix = Matrix4x4.TRS(current.position,
						Quaternion.FromToRotation(Vector3.up, current.parent.position - current.position),
						new Vector3(scale, Vector3.Distance(current.parent.position, current.position), scale)
					);
					Handles.DrawWireCube(Vector3.up * 0.5f, Vector3.one);
					current = current.parent;
				}
				Handles.matrix = matrix;

				if(controlLinesCache == null || (controllersProp.arraySize + 2) != controlLinesCache.Length)
				{
					controlLinesCache = new Vector3[controllersProp.arraySize + 2];
				}
				controlLinesCache[0] = current.position;
				for(var i = 0; i < controllersProp.arraySize; i++)
				{
					var prop = controllersProp.GetArrayElementAtIndex(i);
					controlLinesCache[i + 1] = TransformRooted(bones[0], prop.vector3Value);
				}
				controlLinesCache[controlLinesCache.Length - 1] = TransformRooted(bones[0], targetProp.vector3Value);
				Handles.color = Color.yellow;
				Handles.DrawAAPolyLine(controlLinesCache);
			}

			bool toolActive = IsToolActive();
			if(toolActive)
			{
				var prop = positionControl < 0 ? targetProp : controllersProp.GetArrayElementAtIndex(positionControl);
				var position = prop.vector3Value;
				EditorGUI.BeginChangeCheck();
				position = Handles.PositionHandle(TransformRooted(bones[0], position), Quaternion.identity);
				if(EditorGUI.EndChangeCheck())
				{
					prop.vector3Value = InverseTransformRooted(bones[0], position);
				}
			}

			if(DoButtonHandle(TransformRooted(bones[0], targetProp.vector3Value), Color.red, toolActive && positionControl < 0))
			{
				SetPositionControl(-1);
			}

			for(var i = 0; i < controllersProp.arraySize; i++)
			{
				var prop = controllersProp.GetArrayElementAtIndex(i);
				if(DoButtonHandle(TransformRooted(bones[0], prop.vector3Value), Color.blue, toolActive && positionControl == i))
				{
					SetPositionControl(i);
				}
			}
			Handles.color = oldColor;
			serializedObject.ApplyModifiedProperties();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(affectCountProp);
			if(EditorGUI.EndChangeCheck())
			{
				int count = CountBones(affectCountProp.intValue);
				if(count != affectBonesCount)
				{
					affectBonesCount = count;
					affectCountProp.intValue = count;
					var oldBase = bones[0];
					Init();
					var newBase = bones[0];

					targetProp.vector3Value = InverseTransformRooted(newBase, TransformRooted(oldBase, targetProp.vector3Value));
					for(var i = 0; i < controllersProp.arraySize; i++)
					{
						var prop = controllersProp.GetArrayElementAtIndex(i);
						prop.vector3Value = InverseTransformRooted(newBase, TransformRooted(oldBase, prop.vector3Value));
					}
				}
			}

			GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
			EditorGUILayout.PropertyField(targetProp);
			controllersList.DoLayoutList();

			EditorGUILayout.PropertyField(enableAffectProp);

			serializedObject.ApplyModifiedProperties();
		}

		private void Init()
		{
			var bezier = target as BezierBones;
			bones = new Transform[affectBonesCount + 1];

			var element = bezier.transform;
			for(var i = affectBonesCount; i >= 0; i--)
			{
				bones[i] = element;
				element = element.parent;
			}
		}

		private int CountBones(int preferred)
		{
			var bezier = target as BezierBones;
			var element = bezier.transform;
			for(var i = 0; i < preferred; i++)
			{
				if(element.parent == null)
				{
					return i;
				}
				element = element.parent;
			}
			return preferred;
		}

		private void ReinitIfNeeded()
		{
			if(bones == null || (bones.Length + 1) != affectBonesCount)
			{
				Init();
				return;
			}
			for(var i = 1; i < bones.Length; i++)
			{
				if(bones[i].parent != bones[i - 1])
				{
					Init();
					return;
				}
			}
		}

		private Vector3 GetPosition(float weight)
		{
			if(calcResultCache == null || calcResultCache.Length != (controllersProp.arraySize + 2))
			{
				calcResultCache = new Vector3[controllersProp.arraySize + 2];
			}

			calcPointsCache.CopyTo(calcResultCache, 0);
			for(int i = calcResultCache.Length - 1; i > 0; i--)
			{
				Lerp(weight, calcResultCache, i);
			}
			return calcResultCache[0];
		}

		private void UpdatePointsCache()
		{
			if(calcPointsCache == null || calcPointsCache.Length != (controllersProp.arraySize + 2))
			{
				calcPointsCache = new Vector3[controllersProp.arraySize + 2];
			}

			calcPointsCache[0] = bones[0].position;
			for(var i = 0; i < controllersProp.arraySize; i++)
			{
				calcPointsCache[i + 1] = TransformRooted(bones[0], controllersProp.GetArrayElementAtIndex(i).vector3Value);
			}
			calcPointsCache[calcPointsCache.Length - 1] = TransformRooted(bones[0], targetProp.vector3Value);
		}

		private static void Lerp(float weight, Vector3[] poses, int count)
		{
			for(var i = 0; i < count; i++)
			{
				poses[i] = Vector3.LerpUnclamped(poses[i], poses[i + 1], weight);
			}
		}

		private Vector3 TransformRooted(Transform basic, Vector3 pos)
		{
			return basic.parent?.TransformPoint(pos) ?? pos;
		}

		private Vector3 InverseTransformRooted(Transform basic, Vector3 pos)
		{
			return basic.parent?.InverseTransformPoint(pos) ?? pos;
		}

		private bool DoButtonHandle(Vector3 position, Color color, bool selected)
		{
			var size = HandleUtility.GetHandleSize(position) * 0.16f;
			if(selected)
			{
				if(Event.current.type == EventType.Repaint)
				{
					Handles.color = color;
					Handles.CircleHandleCap(0, position, Camera.current.transform.rotation, size, EventType.Repaint);
				}
				return false;
			}
			Handles.color = color;
			return Handles.Button(position, Quaternion.identity, size, size, Handles.SphereHandleCap);
		}

		private void SetPositionControl(int index)
		{
			if(tool == null)
			{
				tool = ScriptableObject.CreateInstance<BezierBonesTool>();
				EditorTools.SetActiveTool(tool);
			}
			else if(!EditorTools.IsActiveTool(tool))
			{
				EditorTools.SetActiveTool(tool);
			}
			positionControl = index;
		}

		private bool IsToolActive()
		{
			return tool != null && EditorTools.IsActiveTool(tool);
		}

		private void DrawControllersHeader(Rect rect)
		{
			GUI.Label(rect, "Controls");
		}

		private void DrawControllerElement(Rect rect, int index, bool isActive, bool isFocused)
		{
			var prop = controllersProp.GetArrayElementAtIndex(index);
			EditorGUI.PropertyField(rect, prop, GUIContent.none);
		}

		private void RemoveControllerElement(ReorderableList list)
		{
			if(IsToolActive() && list.index <= positionControl)
			{
				positionControl = -1;
				EditorTools.RestorePreviousPersistentTool();
			}
			controllersProp.DeleteArrayElementAtIndex(list.index);
		}

		private void AddControllerElement(ReorderableList list)
		{
			int index = list.index;
			Vector3 pos;
			if(list.index < 0)
			{
				index = controllersProp.arraySize;
				if(index > 0)
				{
					pos = (controllersProp.GetArrayElementAtIndex(index - 1).vector3Value + targetProp.vector3Value) * 0.5f;
				}
				else
				{
					pos = targetProp.vector3Value * 0.5f;
				}
			}
			else
			{
				if(index > 0)
				{
					pos = (controllersProp.GetArrayElementAtIndex(index - 1).vector3Value + controllersProp.GetArrayElementAtIndex(index).vector3Value) * 0.5f;
				}
				else
				{
					pos = controllersProp.GetArrayElementAtIndex(index).vector3Value * 0.5f;
				}
			}
			controllersProp.InsertArrayElementAtIndex(index);
			controllersProp.GetArrayElementAtIndex(index).vector3Value = pos;
		}

		[EditorTool("Bezier points editor", typeof(BezierBones))]
		private class BezierBonesTool : EditorTool
		{
			public override GUIContent toolbarIcon
			{
				get
				{
					if(btnContent == null)
					{
						btnContent = new GUIContent(EditorGUIUtility.IconContent("EditCollider").image, "Bezier points editor");
					}
					return btnContent;
				}
			}

			private GUIContent btnContent = null;

			public override bool IsAvailable()
			{
				return false;
			}
		}
	}
}