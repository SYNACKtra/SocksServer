using System;

namespace SocksServer
{
	public class QHolder {
		public static QHolder Qs = new QHolder();
		public uint[] Qss;
		public UInt64 res = (ulong)UInt32.MaxValue+1;


		public uint c = 362436;

		public QHolder() {
			this.Qss = new uint[4096];
			for(int iz= 0;iz<4096;iz++)  Qss[iz] = 123;
		}

		public ref uint[] get() {
			return ref Qss;
		}

		public void set(ref uint[] Qs) {
			this.Qss = Qs;
		}

		public bool cStarted = false;
		public DateTime cDT = DateTime.Now;
		public uint C {
			get {
				if(!cStarted) {
					cStarted = true;
					cDT = DateTime.Now;
				}
				c += 1;
				if(c == 0 || c == UInt32.MaxValue && cStarted) {
					c = (uint)(((long)((DateTimeOffset)cDT).ToUnixTimeMilliseconds() % (long)(362436*2))%(long)UInt32.MaxValue);
					cStarted = false;
					cDT = DateTime.Now;
				}
				return c;
			}
		}
	}
	public class avg
	{
		private static avg LastSeed = new avg(UInt32.MaxValue, UInt32.MinValue, true);
		private static bool started = false;

		private static MT19937 l_twister = null;
		private int m_result = (int)QGen;

		public int Result {
			get {
				return m_result;
			}
			set {
				throw new Exception("Nice try, haha.");
			}
		}

		private static uint QGen {
			get {
				uint ret = 0;
				uint ii = 4095;
				ii += 1;
				ii &= 4095;
				if(QHolder.Qs.get().Length==0) {
					DateTime dt = DateTime.Now;
					uint entropy = (uint)((long)(((DateTimeOffset)dt).ToUnixTimeMilliseconds())+(long)((DateTimeOffset)System.Diagnostics.Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds() + (long)31337 + (long)3301)%UInt32.MaxValue;

					float x = float.Parse("0."+entropy.ToString());
					uint babe = 0;
					unsafe {
					    float xhalf = 0.5f * x;
					    int i = *(int*)&x;              // get bits for floating value
					    i = 0x5f375a86 - (i >> 1);      // gives initial guess y0
					    x = *(float*)&i;                // convert bits back to float
					    x = x * (1.5f - xhalf * x * x); // Newton step, repeating increases accuracy
					    babe = (uint)(UInt64.Parse(x.ToString().Replace(".", "")) % UInt32.MaxValue);
					}
					QHolder.Qs.set(ref init_avg(QHolder.Qs.res == (ulong)UInt32.MaxValue+1 ? (entropy % UInt32.MaxValue) : (uint)((ulong)QHolder.Qs.res+(ulong)(started?(uint)LastSeed.Result:babe)/2)));			
				} else {
					ret = avg_cwmcrd(QHolder.Qs.get());
				}
				return ret;
			}
		}

		private static long qGen(uint xx) {
			uint[] Q = new uint[4096];
			int ii = 0;

			Q[0] = xx;
			Q[1] = xx + 0x9e3779b9;
			Q[2] = xx + 0x9e3779b9 + 0x9e3779b9;
			//Q[3] = 0;	

			for (ii = 3; ii < 4096; ii++) {
				Q[ii] ^= (uint)Q[ii - 3];
				Q[ii] ^= (uint)Q[ii - 2];
				Q[ii] ^= (uint)0x9e3779b9;
				Q[ii] ^= (uint)ii;
			}

			ulong t, a = 18782;
			uint i = 4095;
			ulong x, r = 0xfffffffe;
			i += 1;
			i &= 4095;
			t = a * Q[i] + QHolder.Qs.c;
			QHolder.Qs.c = (uint)(t >> 32);
			x = t + QHolder.Qs.c;
			if (x < QHolder.Qs.c) {
				x++;
				QHolder.Qs.c++;
			}
			return (uint)(r - x);
		}

		private static ref uint[] init_avg(uint x)
		{
			uint[] Q = new uint[4096];
			for(int iz= 0;iz<4096;iz++)  Q[iz] = 0;
			int i = 0;

			Q[0] = x;
			Q[1] = x + 0x9e3779b9;
			Q[2] = x + 0x9e3779b9 + 0x9e3779b9;
			Q[3] = 0;	

			for (i = 3; i < 4096; i++) {
				Q[i] ^= (uint)Q[i - 3];
				Q[i] ^= (uint)Q[i - 2];
				Q[i] ^= (uint)0x9e3779b9;
				Q[i] ^= (uint)i;
			}
			return ref Q;
		}

		//public static UInt64 res = (ulong)UInt32.MaxValue + 1;

		public static uint avg_cwmcrd(uint[] Q)
		{

			ulong t = 0;
			DateTime dt = DateTime.Now;
			uint entropy = (uint)((long)(((DateTimeOffset)dt).ToUnixTimeMilliseconds())+(long)((DateTimeOffset)System.Diagnostics.Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds() + (long)31337 + (long)3301 + (long)QHolder.Qs.res)%4096;
			uint i = entropy;
			ulong x = 0, r = 0xfffffffe;

			//i += 1;
			//i &= 4095;


			goBack:
			Console.WriteLine(Q[i]);
			Console.WriteLine(QHolder.Qs.c);
			Console.WriteLine("t gain");
			unsafe {
				t = (18782 * (uint)Q[i]) + QHolder.Qs.c;
			}
			Console.WriteLine("t gain");

			Console.WriteLine("t D");
			QHolder.Qs.c = (uint)(t >> (byte)(entropy%32));
			Console.WriteLine("t F");
			x = t + QHolder.Qs.c;
			if (x < QHolder.Qs.c) {
				Console.WriteLine("C:" + QHolder.Qs.C);
				x--;
			}

			QHolder.Qs.res = (ulong)(r - x) % (ulong)UInt32.MaxValue;
			QHolder.Qs.Qss[i] = (uint)QHolder.Qs.res;
			Console.WriteLine("res: " + Q[i]);
			return (uint)QHolder.Qs.res;
			//Q[i] = (uint)res;
		}

		public avg()
		{
			Generate((uint)4294967295, (uint)0, false);
		}

		public avg(uint high, uint low)
		{
			Generate(high, low, false);
		}

		public avg(uint high, uint low, bool startup)
		{
			Generate(high, low, startup);
		}

		public void Generate(uint high, uint low, bool startup)
		{
			if(low == 0) low = 1 ;
			if(started) startup = false;
			primeFind:
			int avgom = (int)(new MT19937(QGen).genavg_int32());
			int avga = 0;
			bool inited = false;
			int jumps = 0;
			int newRandom = avgom;
			int newRandom2 = avgom;

			if(IsPrime(avgom)) {
				int jump;
				startAgain:
				if(started) startup = false;
				uint gen = QGen;
				try {
					BigInteger searchSpace = new BigInteger(UInt32.MaxValue) * new BigInteger(UInt32.MaxValue);
					jump = int.Parse((searchSpace % gen).ToString());
				} catch {
					Console.WriteLine("DBZ");
					goto startAgain;
				}
				newRandom += jump;
				newRandom2 -= jump;
				//int newRandom = avgom + (int)jumps;
				//int newRandom2 = avgom - (int)jumps;
				Console.WriteLine("jump:" + jump);
				Console.WriteLine(newRandom);
				Console.WriteLine(newRandom2);
				if(IsPrime(newRandom) && newRandom != avgom) {
					int result = newRandom - avgom;
					//if(result < 0) result *= -1;
					this.m_result = result;
					LastSeed = this;
					if(startup) started = true;
					return;
				} else if(IsPrime(newRandom2) && newRandom2 != avgom) {
					int result = avgom - newRandom2;
					//if(result < 0) result *= -1;
					this.m_result = result;
					LastSeed = this;
					if(startup) started = true;
					return;
				} else {
					goto startAgain;
				}
			} else {
				goto primeFind;
			}
		}
		
		public static bool IsPrime(int number)
		{
		    if (number <= 1) return false;
		    if (number == 2) return true;
		    if (number % 2 == 0) return false;

		    var boundary = (int)Math.Floor(Math.Sqrt(number));
		          
		    for (int i = 3; i <= boundary; i += 2)
		        if (number % i == 0)
		            return false;
		    
		    return true;        
		} 
	}	
	/// <summary>
	/// Summary description for MT19937.
	/// </summary>
	public class MT19937
	{
		// Period parameters
		private const ulong	N				= 624;
		private const ulong	M				= 397;
		private const ulong	MATRIX_A		= 0x9908B0DFUL;		// constant vector a 
		private const ulong UPPER_MASK		= 0x80000000UL;		// most significant w-r bits
		private const ulong LOWER_MASK		= 0X7FFFFFFFUL;		// least significant r bits
		private const uint	DEFAULT_SEED	= 4357;

		private static ulong [] mt			= new ulong[N+1];	// the array for the state vector
		private static ulong	mti			= N + 1;			// mti==N+1 means mt[N] is not initialized
		private uint rdSeed = 0;

		public MT19937(uint rdSeed)
		{
			this.rdSeed = rdSeed;
			ulong [] init = new ulong[4];
			init[0]= (rdSeed % 2) + (rdSeed % 3);
			init[1]=  (rdSeed % 3) + (rdSeed % 4);
			init[2]= (rdSeed % 4) + (rdSeed % 5);
			init[3] =  (rdSeed % 5) + (rdSeed % 6);
			ulong length = 4;
			init_by_array(init, length);
		}

		// initializes mt[N] with a seed
		void init_genavg(ulong s)
		{
			mt[0]= s & 0xffffffffUL;
			for (mti=1; mti < N; mti++) 
			{
				mt[mti] = (1812433253UL * (mt[mti-1] ^ (mt[mti-1] >> 30)) + mti); 
				/* See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier. */
				/* In the previous versions, MSBs of the seed affect   */
				/* only MSBs of the array mt[].                        */
				/* 2002/01/09 modified by Makoto Matsumoto             */
				mt[mti] &= 0xffffffffUL;
				/* for >32 bit machines */
			}
		}


		// initialize by an array with array-length
		// init_key is the array for initializing keys
		// key_length is its length
		public void init_by_array(ulong[] init_key, ulong key_length)
		{
			ulong i, j, k;
			init_genavg(19650218UL);
			i=1; j=0;
			k = ( N > key_length ? N : key_length);
			for (; k > 0; k--) 
			{
				mt[i] = (mt[i] ^ ((mt[i-1] ^ (mt[i-1] >> 30)) * 1664525UL))
				+ init_key[j] + j;		// non linear 
				mt[i] &= 0xffffffffUL;	// for WORDSIZE > 32 machines
				i++; j++;
				if (i>=N) { mt[0] = mt[N-1]; i=1; }
				if (j>=key_length) j=0;
			}
			for (k = N - 1; k > 0; k--) 
			{
				mt[i] = (mt[i] ^ ((mt[i-1] ^ (mt[i-1] >> 30)) * 1566083941UL))
				- i;					// non linear
				mt[i] &= 0xffffffffUL;	// for WORDSIZE > 32 machines
				i++;
				if (i>=N) { mt[0] = mt[N-1]; i=1; }
			}
			mt[0] = 0x80000000UL;		// MSB is 1; assuring non-zero initial array
		}

		// generates a avgom number on [0,0x7fffffff]-interval
		public long genavg_int31()
		{
			return (long)(genavg_int32()>>1);
		}
		// generates a avgom number on [0,1]-real-interval
		public double genavg_real1()
		{
			return (double)genavg_int32()*(1.0/4294967295.0); // divided by 2^32-1 
		}
		// generates a avgom number on [0,1)-real-interval
		public double genavg_real2()
		{
			return (double)genavg_int32()*(1.0/4294967296.0); // divided by 2^32
		}
		// generates a avgom number on (0,1)-real-interval
		public double genavg_real3()
		{
			return (((double)genavg_int32()) + 0.5)*(1.0/4294967296.0); // divided by 2^32
		}
		// generates a avgom number on [0,1) with 53-bit resolution
		public double genavg_res53() 
		{ 
			ulong a = genavg_int32() >>5;
			ulong b = genavg_int32()>>6; 
			return(double)(a*67108864.0+b)*(1.0/9007199254740992.0); 
		} 
		// These real versions are due to Isaku Wada, 2002/01/09 added 
		
		// generates a avgom number on [0,0xffffffff]-interval
		public ulong genavg_int32()
		{
			ulong y = 0;
			ulong [] mag01 = new ulong[2];
			mag01[0]	= 0x0UL;
			mag01[1]	= MATRIX_A;
			/* mag01[x] = x * MATRIX_A  for x=0,1 */

			if (mti >= N) 
			{ 
				// generate N words at one time
				ulong kk;

				if (mti == N+1)   /* if init_genavg() has not been called, */
					init_genavg(5489UL); /* a default initial seed is used */

				for (kk=0; kk < N - M; kk++) 
				{
					y = (mt[kk]&UPPER_MASK)|(mt[kk+1]&LOWER_MASK);
					mt[kk] = mt[kk+M] ^ (y >> 1) ^ mag01[y & 0x1UL];
				}
				for (;kk<N-1;kk++) 
				{
					y = (mt[kk]&UPPER_MASK)|(mt[kk+1]&LOWER_MASK);
					//mt[kk] = mt[kk+(M-N)] ^ (y >> 1) ^ mag01[y & 0x1UL];
					mt[kk] = mt[kk - 227] ^ (y >> 1) ^ mag01[y & 0x1UL];
				}
				y = (mt[N-1]&UPPER_MASK)|(mt[0]&LOWER_MASK);
				mt[N-1] = mt[M-1] ^ (y >> 1) ^ mag01[y & 0x1UL];

				mti = 0;
			}
		  
			y = mt[mti++];

			/* Tempering */
			y ^= (y >> 11);
			y ^= (y << 7) & 0x9d2c5680UL;
			y ^= (y << 15) & 0xefc60000UL;
			y ^= (y >> 18);

			return y;
		}

		public int RandomRange(int lo, int hi)
		{		
			return (Math.Abs((int)genavg_int32() % (hi - lo + 1)) + lo);
		}
		//public int RollDice(int face, int number_of_dice)
		//{
		//	int roll = 0;
		//	for(int loop=0; loop < number_of_dice; loop++)
		//	{
		//		roll += (RandomRange(1,face));
		//	}
		//	return roll;
		//}
		//public int D6(int die_count)	{ return RollDice(6,die_count); }

	}

	public class RandomMT
	{
		private const int	N				= 624;
		private const int	M				= 397;
		private const uint	K				= 0x9908B0DFU;
		private const uint	DEFAULT_SEED	= 4357;
        
		private ulong []	state			= new ulong[N+1];
		private int			next			= 0;
		private ulong		seedValue;


		public RandomMT()
		{
			SeedMT(DEFAULT_SEED);
		}

		public RandomMT(ulong _seed)
		{
			seedValue = _seed;
			SeedMT(seedValue);
		}

		public ulong RandomInt()
		{
			ulong y;

			if((next + 1) > N)
				return(ReloadMT());

			y  = state[next++];
			y ^= (y >> 11);
			y ^= (y <<  7) & 0x9D2C5680U;
			y ^= (y << 15) & 0xEFC60000U;
			return(y ^ (y >> 18));
		}

		private void SeedMT(ulong _seed)
		{
			ulong x = (_seed | 1U) & 0xFFFFFFFFU;
			int j = N;

			for(j = N; j >=0; j--)
			{
				state[j] = (x*=69069U) & 0xFFFFFFFFU;
			}
			next = 0;
		}

		public int RandomRange(int lo, int hi)
		{		
			return (Math.Abs((int)RandomInt() % (hi - lo + 1)) + lo);
		}

		public int RollDice(int face, int number_of_dice)
		{
			int roll = 0;
			for(int loop=0; loop < number_of_dice; loop++)
			{
				roll += (RandomRange(1,face));
			}
			return roll;
		}

		public int HeadsOrTails()		{ return((int)(RandomInt()) % 2); }

		public int D6(int die_count)	{ return RollDice(6,die_count); }
		public int D8(int die_count)	{ return RollDice(8,die_count); }
		public int D10(int die_count)	{ return RollDice(10,die_count); }
		public int D12(int die_count)	{ return RollDice(12,die_count); }
		public int D20(int die_count)	{ return RollDice(20,die_count); }
		public int D25(int die_count)	{ return RollDice(25,die_count); }


		private ulong ReloadMT()
		{
			ulong [] p0 = state;
			int p0pos = 0;
			ulong [] p2 = state;
			int p2pos = 2;
			ulong [] pM = state;
			int pMpos = M;
			ulong s0;
			ulong s1;

			int    j;

			if((next + 1) > N)
				SeedMT(seedValue);

			for(s0=state[0], s1=state[1], j=N-M+1; --j > 0; s0=s1, s1=p2[p2pos++])
				p0[p0pos++] = pM[pMpos++] ^ (mixBits(s0, s1) >> 1) ^ (loBit(s1) != 0 ? K : 0U);


			for(pM[0]=state[0],pMpos=0, j=M; --j > 0; s0=s1, s1=p2[p2pos++])
				p0[p0pos++] = pM[pMpos++] ^ (mixBits(s0,s1) >> 1) ^ (loBit(s1) != 0 ? K : 0U);
			

			s1=state[0];
			p0[p0pos] = pM[pMpos] ^ (mixBits(s0, s1) >> 1) ^ (loBit(s1) != 0 ? K : 0U);
			s1 ^= (s1 >> 11);
			s1 ^= (s1 <<  7) & 0x9D2C5680U;
			s1 ^= (s1 << 15) & 0xEFC60000U;
			return(s1 ^ (s1 >> 18));
		}

		private ulong hiBit(ulong _u)
		{
			return((_u) & 0x80000000U);
		}
		private ulong loBit(ulong _u)
		{
			return((_u) & 0x00000001U);
		}
		private ulong loBits(ulong _u)
		{
			return((_u) & 0x7FFFFFFFU);
		}
		private ulong mixBits(ulong _u, ulong _v)
		{
			return(hiBit(_u)|loBits(_v));
		}
	}
}