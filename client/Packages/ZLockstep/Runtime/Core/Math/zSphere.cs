namespace zUnity
{
	/// <summary>
	/// 球体结构体
	/// </summary>
	public struct zSphere
	{
		/// <summary>
		/// 中心点
		/// </summary>
		public zVector3 center;

		/// <summary>
		/// 半径
		/// </summary>
		public zfloat radius;

		public zSphere(zVector3 __center, zfloat __radius)
		{
			this.center = __center;
			this.radius = __radius;
		}

		public zVector3 GetCenter()
		{
			return center;
		}

		public zfloat GetRadiusSquare()
		{
			return radius * radius;
		}
	}
}
