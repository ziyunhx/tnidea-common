﻿/// <summary>
/// A .NET Support Vector Machine library adapted from libsvm
/// Copyright ©  Matthew Johnson 2009
/// </summary>
namespace SVM
{
	internal class SVC_Q : Kernel
	{
		private sbyte[] y;

		private Cache cache;

		private float[] QD;

		public SVC_Q(Problem prob, Parameter param, sbyte[] y_)
			: base(prob.Count, prob.X, param)
		{
			this.y = (sbyte[])y_.Clone();
			this.cache = new Cache(prob.Count, (long)(param.CacheSize * 1048576.0));
			this.QD = new float[prob.Count];
			for (int i = 0; i < prob.Count; i++)
			{
				this.QD[i] = (float)base.KernelFunction(i, i);
			}
		}

		public sealed override float[] GetQ(int i, int len)
		{
			float[] array = null;
			int data;
			if ((data = this.cache.GetData(i, ref array, len)) < len)
			{
				for (int j = data; j < len; j++)
				{
					array[j] = (float)((double)(this.y[i] * this.y[j]) * base.KernelFunction(i, j));
				}
			}
			return array;
		}

		public sealed override float[] GetQD()
		{
			return this.QD;
		}

		public sealed override void SwapIndex(int i, int j)
		{
			this.cache.SwapIndex(i, j);
			base.SwapIndex(i, j);
			this.y.SwapIndex(i, j);
			this.QD.SwapIndex(i, j);
		}
	}
}
