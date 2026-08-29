// UltimateStormAPI SC 1.70 compatibility fix v2
// v1: repairs missed Condition PRM SC hook/bounds for this SC 1.70 executable layout.
// v2: additionally preserves an already-existing D-Pad capability across Awakening
//      for characters present in UltimateStormAPI specialCondParam mappings.
// Generic policy: learn F30=1 while non-awakened; only during the early awakening
// transition window, restore F30 if the engine resets it. No character/mod IDs hardcoded.

using u8=unsigned char; using u16=unsigned short; using u32=unsigned int; using u64=unsigned long long;
using i32=int; using i64=long long; using usize=unsigned long long; using DWORD=u32; using BOOL=int; using HANDLE=void*;
#define WINAPI __stdcall
#define EXPORT extern "C" __declspec(dllexport)
extern "C" void* memset(void*d,int c,usize n){u8*p=(u8*)d;for(usize i=0;i<n;i++)p[i]=(u8)c;return d;}
extern "C" void* memcpy(void*d,const void*s,usize n){u8*a=(u8*)d;const u8*b=(const u8*)s;for(usize i=0;i<n;i++)a[i]=b[i];return d;}
struct ListEntry{ListEntry*Flink;ListEntry*Blink;};

static u8 up(u8 c){return(c>='a'&&c<='z')?c-32:c;}
static int eq(const char*a,const char*b){while(*a&&*b){if(*a++!=*b++)return 0;}return *a==*b;}
static int eqwa(const u16*w,const char*a){if(!w||!a)return 0;while(*w&&*a){if(up((u8)*w)!=up((u8)*a))return 0;w++;a++;}return !*w&&!*a;}
static u8* peb(){u8*p=0;__asm__("movq %%gs:0x60,%0":"=r"(p));return p;}
static u8* firstmod(){u8*p=peb();if(!p)return 0;u8*l=*(u8**)(p+0x18);if(!l)return 0;ListEntry*h=(ListEntry*)(l+0x20);auto*c=h->Flink;if(!c||c==h)return 0;return *(u8**)((u8*)c-0x10+0x30);}
static u8* findmod(const char*n){u8*p=peb();if(!p)return 0;u8*l=*(u8**)(p+0x18);if(!l)return 0;ListEntry*h=(ListEntry*)(l+0x20);for(auto*c=h->Flink;c&&c!=h;c=c->Flink){u8*e=(u8*)c-0x10;u8*b=*(u8**)(e+0x30);u16*bn=*(u16**)(e+0x60);if(b&&bn&&eqwa(bn,n))return b;}return 0;}
static u32 pesize(u8*b){if(!b||*(u16*)b!=0x5A4D)return 0;u8*n=b+*(u32*)(b+0x3c);if(*(u32*)n!=0x4550)return 0;return *(u32*)(n+24+56);}
static u32 pets(u8*b){if(!b||*(u16*)b!=0x5A4D)return 0;u8*n=b+*(u32*)(b+0x3c);if(*(u32*)n!=0x4550)return 0;return *(u32*)(n+8);}
static u8* resolve(u8*b,const char*n){if(!b||!n||*(u16*)b!=0x5A4D)return 0;u8*nt=b+*(u32*)(b+0x3c);u8*op=nt+24;u32 er=*(u32*)(op+112),es=*(u32*)(op+116);if(!er)return 0;u8*ed=b+er;u32 nf=*(u32*)(ed+20),nn=*(u32*)(ed+24);u32*fs=(u32*)(b+*(u32*)(ed+28));u32*ns=(u32*)(b+*(u32*)(ed+32));u16*os=(u16*)(b+*(u32*)(ed+36));for(u32 i=0;i<nn;i++){const char*nm=(const char*)(b+ns[i]);if(eq(nm,n)){u16 o=os[i];if(o>=nf)return 0;u32 r=fs[o];if(r>=er&&r<er+es)return 0;return b+r;}}return 0;}

static u64 g_exe=0,g_api=0,g_arg=0;
static u32 g_exeTs=0,g_exeSz=0,g_apiTs=0,g_apiSz=0;
static u32 g_status=0,g_count=0,g_oldMax=0,g_oldCount=0,g_newMax=0,g_newCount=0,g_mapCount=0;
static int g_hookState=0;
static u64 g_lookup=0,g_apiLookup=0,g_limitMaxAddr=0,g_limitCountAddr=0;

static u64 g_ctrlRelay=0,g_ctrlDetour=0,g_ctrlSlot=0;
static u32 g_ctrlStatus=0;
static volatile u32 g_restoreCount=0,g_learnCount=0,g_lastRestoreChar=0,g_lastRestoreAwa=0;

// SC charcodes are small; reserve broad fixed table without CRT allocation.
// seenNormal: D-Pad gate was observed active while non-awakened.
// lastAwake: last EA0 boolean, grace: early-awakening frames where reset recovery is allowed.
struct DState{u8 seenNormal,lastAwake;u16 grace;};
static DState g_ds[4096];

static u32 atomic_inc(volatile u32*p){u32 v=1;__asm__ __volatile__("lock xaddl %0,%1":"+r"(v),"+m"(*p)::"memory");return v;}
static int protect(void*p,usize n,u32*old){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using VP=BOOL(WINAPI*)(void*,usize,u32,u32*);VP f=k?(VP)resolve(k,"VirtualProtect"):0;return f&&f(p,n,0x40,old);}
static void restore(void*p,usize n,u32 old){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using VP=BOOL(WINAPI*)(void*,usize,u32,u32*);VP f=k?(VP)resolve(k,"VirtualProtect"):0;u32 t=0;if(f)f(p,n,old,&t);}
static void flush(void*p,usize n){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using GP=HANDLE(WINAPI*)();using FI=BOOL(WINAPI*)(HANDLE,const void*,usize);GP gp=k?(GP)resolve(k,"GetCurrentProcess"):0;FI fi=k?(FI)resolve(k,"FlushInstructionCache"):0;if(gp&&fi)fi(gp(),p,n);}
static int wr32(u32*p,u32 v){u32 old=0;if(!protect(p,4,&old))return 0;*p=v;flush(p,4);restore(p,4,old);return 1;}
static int wr64(u64*p,u64 v){u32 old=0;if(!protect(p,8,&old))return 0;*p=v;flush(p,8);restore(p,8,old);return 1;}
static int hook_abs(u8*p,u64 dst){u8 patch[20];for(int i=0;i<20;i++)patch[i]=0x90;patch[0]=0xFF;patch[1]=0x25;patch[2]=patch[3]=patch[4]=patch[5]=0;*(u64*)(patch+6)=dst;u32 old=0;if(!protect(p,20,&old))return 0;memcpy(p,patch,20);flush(p,20);restore(p,20,old);return 1;}
static int prefix(const u8*p,const u8*q,usize n){for(usize i=0;i<n;i++)if(p[i]!=q[i])return 0;return 1;}

static u64 decode_e9(u8*p){if(!p||p[0]!=0xE9)return 0;i32 d=*(i32*)(p+1);return(u64)(p+5+d);}
static u64* relay_slot(u64 a){if(!a)return 0;u8*p=(u8*)a;if(p[0]==0xFF&&p[1]==0x25){i32 d=*(i32*)(p+2);return(u64*)(p+6+d);}if(p[0]==0x48&&p[1]==0xB8&&p[10]==0xFF&&p[11]==0xE0)return(u64*)(p+2);return 0;}
static u64 chase_relay(u64 a){u64*s=relay_slot(a);return s?*s:a;}

static int is_special_char(u32 cc){
  if(!g_api)return 0;
  u64 b=*(u64*)(g_api+0xF1380),e=*(u64*)(g_api+0xF1388);
  if(!b||!e||e<b||((e-b)&3))return 0;
  u64 n=(e-b)>>2;if(n>8192)n=8192;
  u32*p=(u32*)b;for(u64 i=0;i<n;i++)if(p[i]==cc)return 1;
  return 0;
}

typedef u64(__fastcall*CtrlFn)(u64);
extern "C" u64 __fastcall wrapCtrl(u64 p){
  if(p){
    u8*b=(u8*)p;u32 cc=*(u32*)(b+0xE64);u32 awa=*(u32*)(b+0xEA0);u32 gate=*(u32*)(b+0xF30);
    if(cc<4096 && is_special_char(cc)){
      DState&s=g_ds[cc];u8 aw=awa?1:0;
      if(!aw){
        s.grace=0;s.lastAwake=0;
        if(gate==1 && !s.seenNormal){s.seenNormal=1;atomic_inc(&g_learnCount);}
      }else{
        if(!s.lastAwake){s.grace=240;s.lastAwake=1;} // only early transition window (~few seconds)
        if(s.grace){
          if(gate==0 && s.seenNormal){
            *(u32*)(b+0xF30)=1;
            atomic_inc(&g_restoreCount);g_lastRestoreChar=cc;g_lastRestoreAwa=awa;
          }
          s.grace--;
        }
      }
    }
  }
  return ((CtrlFn)g_ctrlDetour)(p);
}

static u32 install_condition_fix(){
  u8*exe=firstmod();if(!exe)return 1;g_exe=(u64)exe;g_exeTs=pets(exe);g_exeSz=pesize(exe);
  if(g_exeTs!=0x6A507172u||g_exeSz!=0x09E85000u)return 2;
  u8*api=findmod("d3dcompiler_47.dll");if(!api)api=findmod("D3DCOMPILER_47.DLL");if(!api)return 3;
  g_api=(u64)api;g_apiTs=pets(api);g_apiSz=pesize(api);if(g_apiTs!=0x6A8DF498u||g_apiSz!=0x000FC000u)return 4;
  u64 beg=*(u64*)(api+0xF0E48),end=*(u64*)(api+0xF0E50);
  if(!beg||!end||end<beg||((end-beg)&31))return 5;
  u64 cnt=(end-beg)>>5;if(cnt<512||cnt>4096)return 6;g_count=(u32)cnt;
  u64 mb=*(u64*)(api+0xF1380),me=*(u64*)(api+0xF1388);if(mb&&me>=mb&&((me-mb)&3)==0)g_mapCount=(u32)((me-mb)>>2);
  g_lookup=g_exe+0x9887E0;g_apiLookup=g_api+0x8450;g_limitMaxAddr=g_exe+0x97CAEB;g_limitCountAddr=g_exe+0x9AE451;
  u8*lookup=(u8*)g_lookup;
  const u8 expected1[9]={0x8D,0x41,0xFF,0x3D,0xFE,0x01,0x00,0x00,0x77};
  const u8 expected2[7]={0x81,0xFA,0xFF,0x01,0x00,0x00,0x77};
  const u8 expected3[7]={0x81,0xFB,0x00,0x02,0x00,0x00,0x7C};
  if(!(prefix(lookup,expected1,9)||(lookup[0]==0xFF&&lookup[1]==0x25)))return 7;
  // Accept either pristine legacy immediates or values already expanded by v1.
  u8*pm=(u8*)(g_limitMaxAddr-2),*pc=(u8*)(g_limitCountAddr-2);
  if(!(pm[0]==0x81&&pm[1]==0xFA&&pm[6]==0x77))return 8;
  if(!(pc[0]==0x81&&pc[1]==0xFB&&pc[6]==0x7C))return 9;
  g_oldMax=*(u32*)g_limitMaxAddr;g_oldCount=*(u32*)g_limitCountAddr;g_newMax=g_count-1;g_newCount=g_count;
  if(!(lookup[0]==0xFF&&lookup[1]==0x25)){if(!hook_abs(lookup,g_apiLookup))return 10;g_hookState=1;}else g_hookState=2;
  if(g_oldMax!=g_newMax&&!wr32((u32*)g_limitMaxAddr,g_newMax))return 11;
  if(g_oldCount!=g_newCount&&!wr32((u32*)g_limitCountAddr,g_newCount))return 12;
  return 0;
}

static u32 install_ctrl_fix(){
  if(!g_exe||!g_api)return 20;
  u8*ct=(u8*)(g_exe+0xA346F0);g_ctrlRelay=decode_e9(ct);if(!g_ctrlRelay)return 21;
  u64*slot=relay_slot(g_ctrlRelay);if(!slot)return 22;
  u64 det=chase_relay(g_ctrlRelay);if(!det)return 23;
  // If already pointing to us, accept idempotently.
  if(*slot==(u64)&wrapCtrl){g_ctrlDetour=det;g_ctrlSlot=(u64)slot;return 0;}
  if(det<g_api||det>=g_api+g_apiSz)return 24;
  g_ctrlDetour=det;g_ctrlSlot=(u64)slot;
  if(!wr64(slot,(u64)&wrapCtrl))return 25;
  return 0;
}

static char out[12288];static u32 op=0;
static void pc1(char c){if(op+1<sizeof(out))out[op++]=c;}static void ps(const char*s){if(!s)return;while(*s&&op+1<sizeof(out))out[op++]=*s++;}
static char hx(u8 v){v&=15;return v<10?'0'+v:'A'+v-10;}static void h64(u64 v){ps("0x");for(int i=15;i>=0;i--)pc1(hx((u8)(v>>(i*4))));}static void h32(u32 v){ps("0x");for(int i=7;i>=0;i--)pc1(hx((u8)(v>>(i*4))));}
static void du(u32 v){char z[16];int n=0;if(!v){pc1('0');return;}while(v){z[n++]='0'+v%10;v/=10;}while(n)pc1(z[--n]);}static void nl(){pc1('\r');pc1('\n');}
static void buildlog(){
  op=0;ps("NSC UltimateStormAPI SC 1.70 Compatibility Fix v2");nl();nl();
  ps("EXE base: ");h64(g_exe);ps(" ts=");h32(g_exeTs);ps(" size=");h32(g_exeSz);nl();
  ps("API base: ");h64(g_api);ps(" ts=");h32(g_apiTs);ps(" size=");h32(g_apiSz);nl();
  ps("Condition status: ");if(!g_status)ps("CONDITION_COMPAT_FIX_APPLIED");else{ps("FAILED code=");du(g_status);}nl();
  ps("Runtime condition count: ");du(g_count);nl();ps("Special-condition mappings loaded: ");du(g_mapCount);nl();
  ps("Lookup target: ");h64(g_lookup);ps(" -> API helper ");h64(g_apiLookup);ps(" hookState=");du(g_hookState);nl();
  ps("Max-index immediate: ");h64(g_limitMaxAddr);ps(" old=");h32(g_oldMax);ps(" new=");h32(g_newMax);nl();
  ps("Count immediate: ");h64(g_limitCountAddr);ps(" old=");h32(g_oldCount);ps(" new=");h32(g_newCount);nl();nl();
  ps("Awakening D-Pad persistence: ");if(!g_ctrlStatus)ps("CTRL_CHAIN_OK");else{ps("FAILED code=");du(g_ctrlStatus);}nl();
  ps("Ctrl relay: ");h64(g_ctrlRelay);ps(" original API detour: ");h64(g_ctrlDetour);ps(" slot: ");h64(g_ctrlSlot);nl();
  ps("Learned normal D-Pad characters: ");du(g_learnCount);nl();
  ps("Awakening F30 restorations: ");du(g_restoreCount);nl();
  ps("Last restored charcode: ");du(g_lastRestoreChar);ps(" (hex ");h32(g_lastRestoreChar);ps(") EA0=");h32(g_lastRestoreAwa);nl();
  ps("Policy: only specialCond-mapped characters already observed with F30=1 before Awakening are eligible; recovery is limited to the early Awakening transition window.");nl();
  ps("No character/mod IDs are hardcoded.");nl();out[op]=0;
}
static void writelog(){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using CF=HANDLE(WINAPI*)(const char*,u32,u32,void*,u32,u32,HANDLE);using WF=BOOL(WINAPI*)(HANDLE,const void*,u32,u32*,void*);using CH=BOOL(WINAPI*)(HANDLE);CF cf=k?(CF)resolve(k,"CreateFileA"):0;WF wf=k?(WF)resolve(k,"WriteFile"):0;CH ch=k?(CH)resolve(k,"CloseHandle"):0;if(!cf||!wf||!ch)return;HANDLE h=cf("moddingapi\\api_condition_compat_fix_v2.log",0x40000000,3,0,2,0x80,0);if((i64)h==-1)h=cf("api_condition_compat_fix_v2.log",0x40000000,3,0,2,0x80,0);if((i64)h==-1)return;u32 w=0;wf(h,out,op,&w,0);ch(h);}

static DWORD WINAPI worker(void*){
  u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using SL=void(WINAPI*)(u32);SL sl=k?(SL)resolve(k,"Sleep"):0;
  if(sl)sl(5000);
  for(int i=0;i<40;i++){u8*api=findmod("d3dcompiler_47.dll");if(api){u64 b=*(u64*)(api+0xF0E48),e=*(u64*)(api+0xF0E50);if(b&&e>b)break;}if(sl)sl(250);}
  g_status=install_condition_fix();
  g_ctrlStatus=g_status?26:install_ctrl_fix();
  for(int i=0;i<1800;i++){if((i%4)==0){buildlog();writelog();}if(sl)sl(500);} // keep runtime diagnostics fresh ~15 min
  buildlog();writelog();return 0;
}
static void start(){u8*k=findmod("KERNELBASE.DLL");if(!k)k=findmod("KERNEL32.DLL");using CT=HANDLE(WINAPI*)(void*,usize,DWORD(WINAPI*)(void*),void*,u32,u32*);using CH=BOOL(WINAPI*)(HANDLE);CT ct=k?(CT)resolve(k,"CreateThread"):0;CH ch=k?(CH)resolve(k,"CloseHandle"):0;if(!ct)return;u32 tid=0;HANDLE h=ct(0,0,worker,0,0,&tid);if(h&&(i64)h!=-1&&ch)ch(h);}
EXPORT void WINAPI InitializePlugin(u64 moduleBase){g_arg=moduleBase;start();}
EXPORT void WINAPI GameLoop(){}
EXPORT BOOL WINAPI DllMain(void*,DWORD,void*){return 1;}
