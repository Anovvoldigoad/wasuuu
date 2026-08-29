// UltimateStormAPI SC 1.70 Condition PRM compatibility fix v1
// Generic fix for the user's NSUNSC 1.70 executable fingerprint.
// Problem: API's SC Condition PRM signatures expect legacy limits 0x1FB/0x1FC/0x1FD,
// while this executable uses 0x1FE/0x1FF/0x200. The signatures therefore miss,
// so the dynamic condition lookup hook and limit patches are never installed.
// This plugin installs the same intended lookup behavior using API+0x8450 and derives
// limits from UltimateStormAPI's runtime condition vector. No Tobi-specific IDs/names.

using u8=unsigned char; using u16=unsigned short; using u32=unsigned int; using u64=unsigned long long;
using i32=int; using i64=long long; using usize=unsigned long long; using DWORD=u32; using BOOL=int; using HANDLE=void*;
#define WINAPI __stdcall
#define EXPORT extern "C" __declspec(dllexport)
extern "C" void* memset(void*d,int c,usize n){u8*p=(u8*)d;for(usize i=0;i<n;i++)p[i]=(u8)c;return d;}
extern "C" void* memcpy(void*d,const void*s,usize n){u8*a=(u8*)d;const u8*b=(const u8*)s;for(usize i=0;i<n;i++)a[i]=b[i];return d;}
struct ListEntry{ListEntry*Flink;ListEntry*Blink;};
static u8 up(u8 c){return(c>='a'&&c<='z')?c-32:c;} static int eq(const char*a,const char*b){while(*a&&*b){if(*a++!=*b++)return 0;}return *a==*b;} static int eqwa(const u16*w,const char*a){if(!w||!a)return 0;while(*w&&*a){if(up((u8)*w)!=up((u8)*a))return 0;w++;a++;}return !*w&&!*a;}
static u8* peb(){u8*p=0;__asm__("movq %%gs:0x60,%0":"=r"(p));return p;}
static u8* firstmod(){u8*p=peb();if(!p)return 0;u8*l=*(u8**)(p+0x18);if(!l)return 0;ListEntry*h=(ListEntry*)(l+0x20);auto*c=h->Flink;if(!c||c==h)return 0;return *(u8**)((u8*)c-0x10+0x30);}
static u8* findmod(const char*n){u8*p=peb();if(!p)return 0;u8*l=*(u8**)(p+0x18);if(!l)return 0;ListEntry*h=(ListEntry*)(l+0x20);for(auto*c=h->Flink;c&&c!=h;c=c->Flink){u8*e=(u8*)c-0x10;u8*b=*(u8**)(e+0x30);u16*bn=*(u16**)(e+0x60);if(b&&bn&&eqwa(bn,n))return b;}return 0;}
static u32 pesize(u8*b){if(!b||*(u16*)b!=0x5A4D)return 0;u8*n=b+*(u32*)(b+0x3c);if(*(u32*)n!=0x4550)return 0;return *(u32*)(n+24+56);} static u32 pets(u8*b){if(!b||*(u16*)b!=0x5A4D)return 0;u8*n=b+*(u32*)(b+0x3c);if(*(u32*)n!=0x4550)return 0;return *(u32*)(n+8);}
static u8* resolve(u8*b,const char*n){if(!b||!n||*(u16*)b!=0x5A4D)return 0;u8*nt=b+*(u32*)(b+0x3c);u8*op=nt+24;u32 er=*(u32*)(op+112),es=*(u32*)(op+116);if(!er)return 0;u8*ed=b+er;u32 nf=*(u32*)(ed+20),nn=*(u32*)(ed+24);u32*fs=(u32*)(b+*(u32*)(ed+28));u32*ns=(u32*)(b+*(u32*)(ed+32));u16*os=(u16*)(b+*(u32*)(ed+36));for(u32 i=0;i<nn;i++){const char*nm=(const char*)(b+ns[i]);if(eq(nm,n)){u16 o=os[i];if(o>=nf)return 0;u32 r=fs[o];if(r>=er&&r<er+es)return 0;return b+r;}}return 0;}

static u64 g_exe=0,g_api=0,g_arg=0; static u32 g_exeTs=0,g_exeSz=0,g_apiTs=0,g_apiSz=0;
static u32 g_status=0,g_count=0,g_oldMax=0,g_oldCount=0,g_newMax=0,g_newCount=0,g_mapCount=0; static int g_hookState=0;
static u64 g_lookup=0,g_apiLookup=0,g_limitMaxAddr=0,g_limitCountAddr=0;

static int protect(void*p,usize n,u32*old){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using VP=BOOL(WINAPI*)(void*,usize,u32,u32*);VP f=k?(VP)resolve(k,"VirtualProtect"):0;return f&&f(p,n,0x40,old);}
static void restore(void*p,usize n,u32 old){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using VP=BOOL(WINAPI*)(void*,usize,u32,u32*);VP f=k?(VP)resolve(k,"VirtualProtect"):0;u32 t=0;if(f)f(p,n,old,&t);}
static void flush(void*p,usize n){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using GP=HANDLE(WINAPI*)();using FI=BOOL(WINAPI*)(HANDLE,const void*,usize);GP gp=k?(GP)resolve(k,"GetCurrentProcess"):0;FI fi=k?(FI)resolve(k,"FlushInstructionCache"):0;if(gp&&fi)fi(gp(),p,n);}
static int wr32(u32*p,u32 v){u32 old=0;if(!protect(p,4,&old))return 0;*p=v;flush(p,4);restore(p,4,old);return 1;}
static int hook_abs(u8*p,u64 dst){u8 patch[20];for(int i=0;i<20;i++)patch[i]=0x90;patch[0]=0xFF;patch[1]=0x25;patch[2]=patch[3]=patch[4]=patch[5]=0;*(u64*)(patch+6)=dst;u32 old=0;if(!protect(p,20,&old))return 0;memcpy(p,patch,20);flush(p,20);restore(p,20,old);return 1;}

static int prefix(const u8*p,const u8*q,usize n){for(usize i=0;i<n;i++)if(p[i]!=q[i])return 0;return 1;}
static u32 install_fix(){
  u8*exe=firstmod(); if(!exe)return 1; g_exe=(u64)exe; g_exeTs=pets(exe);g_exeSz=pesize(exe);
  if(g_exeTs!=0x6A507172u||g_exeSz!=0x09E85000u)return 2;
  u8*api=findmod("d3dcompiler_47.dll"); if(!api)api=findmod("D3DCOMPILER_47.DLL"); if(!api)return 3;
  g_api=(u64)api;g_apiTs=pets(api);g_apiSz=pesize(api); if(g_apiTs!=0x6A8DF498u||g_apiSz!=0x000FC000u)return 4;
  // API runtime vector std::vector<32-byte condition entries> at F0E48/F0E50.
  u64 beg=*(u64*)(api+0xF0E48), end=*(u64*)(api+0xF0E50);
  if(!beg||!end||end<beg||((end-beg)&31))return 5;
  u64 cnt=(end-beg)>>5; if(cnt<512||cnt>4096)return 6; g_count=(u32)cnt;
  // Special-condition map diagnostics (charcode -> condition index), generic only.
  u64 mb=*(u64*)(api+0xF1380), me=*(u64*)(api+0xF1388); if(mb&&me>=mb&&((me-mb)&3)==0)g_mapCount=(u32)((me-mb)>>2);

  g_lookup=g_exe+0x9887E0; g_apiLookup=g_api+0x8450;
  g_limitMaxAddr=g_exe+0x97CAEB; g_limitCountAddr=g_exe+0x9AE451;
  u8*lookup=(u8*)g_lookup;
  const u8 expected1[9]={0x8D,0x41,0xFF,0x3D,0xFE,0x01,0x00,0x00,0x77};
  const u8 expected2[7]={0x81,0xFA,0xFF,0x01,0x00,0x00,0x77};
  const u8 expected3[7]={0x81,0xFB,0x00,0x02,0x00,0x00,0x7C};
  if(!(prefix(lookup,expected1,9)||(lookup[0]==0xFF&&lookup[1]==0x25)))return 7;
  if(!prefix((u8*)(g_limitMaxAddr-2),expected2,7))return 8;
  if(!prefix((u8*)(g_limitCountAddr-2),expected3,7))return 9;
  g_oldMax=*(u32*)g_limitMaxAddr; g_oldCount=*(u32*)g_limitCountAddr;
  g_newMax=g_count-1; g_newCount=g_count;
  if(!(lookup[0]==0xFF&&lookup[1]==0x25)){if(!hook_abs(lookup,g_apiLookup))return 10;g_hookState=1;}else g_hookState=2;
  if(!wr32((u32*)g_limitMaxAddr,g_newMax))return 11;
  if(!wr32((u32*)g_limitCountAddr,g_newCount))return 12;
  return 0;
}

static char out[8192];static u32 op=0;static void pc(char c){if(op+1<sizeof(out))out[op++]=c;}static void ps(const char*s){if(!s)return;while(*s&&op+1<sizeof(out))out[op++]=*s++;}static char hx(u8 v){v&=15;return v<10?'0'+v:'A'+v-10;}static void h64(u64 v){ps("0x");for(int i=15;i>=0;i--)pc(hx((u8)(v>>(i*4))));}static void h32(u32 v){ps("0x");for(int i=7;i>=0;i--)pc(hx((u8)(v>>(i*4))));}static void du(u32 v){char z[16];int n=0;if(!v){pc('0');return;}while(v){z[n++]='0'+v%10;v/=10;}while(n)pc(z[--n]);}static void nl(){pc('\r');pc('\n');}
static void buildlog(){op=0;ps("NSC UltimateStormAPI SC 1.70 Condition Compatibility Fix v1");nl();nl();ps("EXE base: ");h64(g_exe);ps(" ts=");h32(g_exeTs);ps(" size=");h32(g_exeSz);nl();ps("API base: ");h64(g_api);ps(" ts=");h32(g_apiTs);ps(" size=");h32(g_apiSz);nl();ps("Status: ");if(!g_status)ps("CONDITION_COMPAT_FIX_APPLIED");else{ps("FAILED code=");du(g_status);}nl();ps("Runtime condition count: ");du(g_count);nl();ps("Special-condition mappings loaded: ");du(g_mapCount);nl();ps("Lookup target: ");h64(g_lookup);ps(" -> API helper ");h64(g_apiLookup);ps(" hookState=");du(g_hookState);nl();ps("Max-index immediate: ");h64(g_limitMaxAddr);ps(" old=");h32(g_oldMax);ps(" new=");h32(g_newMax);nl();ps("Count immediate: ");h64(g_limitCountAddr);ps(" old=");h32(g_oldCount);ps(" new=");h32(g_newCount);nl();ps("Expected for this EXE before fix: lookup limit 0x1FE, max immediate 0x1FF, count immediate 0x200.");nl();ps("This patch is generic: values derive from UltimateStormAPI runtime vectors; no character/mod IDs are hardcoded.");nl();out[op]=0;}
static void writelog(){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using CF=HANDLE(WINAPI*)(const char*,u32,u32,void*,u32,u32,HANDLE);using WF=BOOL(WINAPI*)(HANDLE,const void*,u32,u32*,void*);using CH=BOOL(WINAPI*)(HANDLE);CF cf=k?(CF)resolve(k,"CreateFileA"):0;WF wf=k?(WF)resolve(k,"WriteFile"):0;CH ch=k?(CH)resolve(k,"CloseHandle"):0;if(!cf||!wf||!ch)return;HANDLE h=cf("moddingapi\\api_condition_compat_fix.log",0x40000000,3,0,2,0x80,0);if((i64)h==-1)h=cf("api_condition_compat_fix.log",0x40000000,3,0,2,0x80,0);if((i64)h==-1)return;u32 w=0;wf(h,out,op,&w,0);ch(h);}
static DWORD WINAPI worker(void*){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using SL=void(WINAPI*)(u32);SL sl=k?(SL)resolve(k,"Sleep"):0;if(sl)sl(5000); // let API parse param files
  // poll a little longer if vector is not ready
  for(int i=0;i<40;i++){u8*api=findmod("d3dcompiler_47.dll");if(api){u64 b=*(u64*)(api+0xF0E48),e=*(u64*)(api+0xF0E50);if(b&&e>b)break;}if(sl)sl(250);}g_status=install_fix();buildlog();writelog();return 0;}
static void start(){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using CT=HANDLE(WINAPI*)(void*,usize,DWORD(WINAPI*)(void*),void*,u32,u32*);using CH=BOOL(WINAPI*)(HANDLE);CT ct=k?(CT)resolve(k,"CreateThread"):0;CH ch=k?(CH)resolve(k,"CloseHandle"):0;if(!ct)return;u32 tid=0;HANDLE h=ct(0,0,worker,0,0,&tid);if(h&&(i64)h!=-1&&ch)ch(h);}
EXPORT void WINAPI InitializePlugin(u64 moduleBase){g_arg=moduleBase;start();}
EXPORT void WINAPI GameLoop(){}
EXPORT BOOL WINAPI DllMain(void*,DWORD,void*){return 1;}
