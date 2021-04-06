using UnityEditor;
using UnityEngine;

namespace Xytabich.Tools
{
	[ExecuteInEditMode]
	public class BezierBones : MonoBehaviour
	{
		[SerializeField, Min(1)]
		private int affectBonesCount = 2;

		[SerializeField]
		private Vector3[] controllers = new Vector3[0];

		[SerializeField]
		private Vector3 target = Vector3.zero;

		[SerializeField]
		private bool enableBonesAffect = false;

		private Vector3[] originalDirection;
		private Quaternion[] originalRotation;
		private Transform[] bones = null;
		private float[] weights = null;

		private Vector3[] calcPointsCache = null;
		private Vector3[] calcResultCache = null;

		void Awake()
		{
			hideFlags = HideFlags.DontSave;
		}

		void OnEnable()
		{
			EditorApplication.update += OnUpdate;
		}

		void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		private void Init()
		{
			var element = transform;
			for(var i = 0; i < affectBonesCount; i++)
			{
				if(element.parent == null)
				{
					affectBonesCount = i;
					break;
				}
				element = element.parent;
			}

			weights = new float[affectBonesCount];
			bones = new Transform[affectBonesCount + 1];
			originalDirection = new Vector3[affectBonesCount];
			originalRotation = new Quaternion[affectBonesCount];

			float overallLen = 0f;
			element = transform;
			for(var i = affectBonesCount; i >= 0; i--)
			{
				bones[i] = element;
				if(i < affectBonesCount)
				{
					originalRotation[i] = element.rotation;
				}
				element = element.parent;
				if(i > 0)
				{
					var offset = bones[i].position - element.position;
					originalDirection[i - 1] = offset.normalized;
					float len = Vector3.SqrMagnitude(offset);
					overallLen += len;
					weights[i - 1] = len;
				}
			}
			for(var i = 0; i < affectBonesCount; i++)
			{
				weights[i] /= overallLen;
				if(i > 0) weights[i] += weights[i - 1];
			}
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

		private void OnUpdate()
		{
			if(enableBonesAffect)
			{
				ReinitIfNeeded();
				UpdatePointsCache();
				for(var i = 0; i < affectBonesCount; i++)
				{
					var offset = Quaternion.FromToRotation(originalDirection[i], (GetPosition(weights[i]) - bones[i].position).normalized);
					bones[i].rotation = offset * originalRotation[i];
				}
			}
		}

		private Vector3 GetPosition(float weight)
		{
			if(calcResultCache == null || calcResultCache.Length != (controllers.Length + 2))
			{
				calcResultCache = new Vector3[controllers.Length + 2];
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
			if(calcPointsCache == null || calcPointsCache.Length != (controllers.Length + 2))
			{
				calcPointsCache = new Vector3[controllers.Length + 2];
			}

			calcPointsCache[0] = bones[0].position;
			for(var i = 0; i < controllers.Length; i++)
			{
				calcPointsCache[i + 1] = TransformRooted(controllers[i]);
			}
			calcPointsCache[calcPointsCache.Length - 1] = TransformRooted(target);
		}

		private Vector3 TransformRooted(Vector3 pos)
		{
			return bones[0].parent?.TransformPoint(pos) ?? pos;
		}

		private static void Lerp(float weight, Vector3[] poses, int count)
		{
			for(var i = 0; i < count; i++)
			{
				poses[i] = Vector3.LerpUnclamped(poses[i], poses[i + 1], weight);
			}
		}
	}
}