; Assembly listing for method OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ComputeBoundedRegion[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+MatchedLinesKernel](OoplesFinance.StockIndicators.Indicators.Bar[],double[][],OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer,int,int,int,System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.IIndicator):OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+PointwiseRegion (Tier1-OSR)
 ; Emitting BLENDED_CODE for generic X64 + VEX on Windows
; Tier1-OSR code
; OSR variant for entry point 0x3ad

G_M000_IG06:
; optimized code
; optimized using Synthesized PGO
; rsp based frame
; partially interruptible
; with Synthesized PGO: fgCalledCount is 25
; 16 inlinees with PGO data; 44 single block inlinees; 0 inlinees without PGO data
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG07:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG08:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG09:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG10:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG11:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x20], rax
       cmp      gword ptr [rbp-0x20], 0
       je       SHORT G_M000_IG13
       mov      rcx, gword ptr [rbp-0x20]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.WindowLinearRegression:get_Period():int:this]
       cmp      eax, 0x7FFF
       jle      SHORT G_M000_IG12
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG34
 
G_M000_IG12:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG13:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x28], rax
       cmp      gword ptr [rbp-0x28], 0
       je       SHORT G_M000_IG15
       mov      rcx, gword ptr [rbp-0x28]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.LaggedPriceChange:get_Period():int:this]
       cmp      eax, 0xD1FFAB1E
       jl       SHORT G_M000_IG14
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG34
 
G_M000_IG14:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 
G_M000_IG15:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x30], rax
       cmp      gword ptr [rbp-0x30], 0
       je       SHORT G_M000_IG17
       mov      rcx, gword ptr [rbp-0x30]
       cmp      dword ptr [rcx], ecx
       call     
G_M000_IG01:[OoplesFinance.StockIndicators.Indicators.RateOfChangeWithValidity:get_Period():int:this]

       cmp            eax, 0xD1FFAB1E
       jl       SHORT G_M000_IG16
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG34
  sub      rsp, 168

G_M000_IG16:
       mov      qword ptr [rsp+0x258], r15
       mov      qword ptr [       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 rsp+0x250], r14

G_M000_IG17:
       mov      qword ptr [rsp+0x248], r13
       mov      rdx, gword ptr [rbp+0x10]
       mov      qword ptr [       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]rsp+0x240], r12

       mov      qword ptr [rsp+0x238], rdi
             mov      qword ptr [rsp+0x230] test     , rsi
rax, rax
       mov      qword ptr [rsp+0x228], rbx
             mov      r14, qword ptr [rsp+ jne      0x270]
G_M000_IG24
       mov      rsi       mov      , gword ptr [rsp+0x278]
       mov      rbx, gword ptr [rsp+0x288]
rdx, gword ptr [rbp+0x10]
             mov      rcx, 0xD1FFAB1E
 mov            edi, dword ptr [rsp+0x298]
 call     [CORINFO_HELP_ISINSTANCEOFCLASS]
             test     rax, rax
       jne      G_M000_IG23
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [ mov      rbp, bword ptr [rsp+0x2B0]
       mov      r15, gword ptr [rsp+0x1B8]CORINFO_HELP_ISINSTANCEOFCLASS]
       test     rax, rax
       jne      G_M000_IG22

       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       mov             call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       test     rax, rax
       jne      G_M000_IG21
       mov      rdx, gword ptr [rbp+0x10]
r13d, dword ptr [       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
rsp+0x184]
       test     rax, rax
       mov      ecx, dword ptr [rsp+       jne      G_M000_IG20
       mov      rdx, gword ptr [rbp0x11C]
+0x10]
       mov      rcx, 0xD1FFAB1E 
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
      
G_M000_IG02:
 test     rax, rax
       jne      SHORT G_M000_IG19
       mov             mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
r12       mov      gword ptr [rbp, gword ptr [rsp+0x2B8]
-0x38], rax
       mov      rax, gword ptr [       cmp      rsp+0x1C0]
gword ptr [rbp-0x38], 0
             je       G_M000_IG25
       mov      rcx, gword ptr [rbp-0x38]
 mov            gword ptr [rsp+0x20], rax
 cmp      dword ptr [rcx]       mov      r8d, dword ptr [, ecx
       call     rsp+0x20C]
       mov      dword ptr [rsp+0x34], r8d
       mov      [OoplesFinance.StockIndicators.Indicators.PriorPriceChannel:get_Period():int:this]
r10d, ecx
       cmp             cmp      r10d, edi
eax, 0xD1FFAB1E
       jl       SHORT        jl       G_M000_IG08
G_M000_IG18
       mov      rcx,        jmp      G_M000_IG17
 0xD1FFAB1E

G_M000_IG03:
             cmp      gword ptr [rbx+0x10], 0
       jne      G_M000_IG20
 call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG34
        mov      rcx, gword ptr [rbx+0x08]
       mov      
G_M000_IG18:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 edx
G_M000_IG19:
       mov      rcx, 0xD1FFAB1E
, eax
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
        sar      
G_M000_IG20:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 edx
G_M000_IG21:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
, 10
       cmp      edx, dword ptr [       jmp      G_M000_IG29
 rcx+0x08]

G_M000_IG22:
       mov      rcx, 0xD1FFAB1E
       jae             call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 G_M000_IG21

G_M000_IG23:
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx+       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
 8*rdx+0x10]
       mov      edx, eax

G_M000_IG24:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG29
        and      edx, 0x3FF

G_M000_IG25:
       cmp      edx, dword ptr [rcx+0x08]
       jae      G_M000_IG21
       mov      rdx, gword ptr [rbp+0x10]
       imul     rdx, rdx, 48
       mov             lea      rcx, bword ptr [rcx+rdx+rcx, 0xD1FFAB1E
       call     [0x10]
 CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x40], rax
       cmp      gword ptr [rbp-0x40], 0
       je       SHORT G_M000_IG27

G_M000_IG04:
       mov      rcx, gword ptr [rbp-0x40]
             cmp      dword ptr [rcx] vmovdqu  , ecx
ymm1, ymmword ptr [rsp+0x188]
       call            vmovdqu  ymmword ptr [rcx], ymm1
       vmovdqu  xmm1, xmmword ptr [rsp+0x1A8][
OoplesFinance.StockIndicators.Indicators.ClassicDeviationBands:get_Method():int:this]
       vmovdqu  xmmword ptr [rcx+      0x20] test     , xmm1
eax , eax
       je       SHORT G_M000_IG26

G_M000_IG05:
       mov      rcx      , 0xD1FFAB1E vmovq    
rcx, xmm0
       mov      rdx       call     CORINFO_HELP_COUNTPROFILE32
, 0xD1FFAB1E
       jmp      G_M000_IG34
        andn     
G_M000_IG26:
rcx       mov      rcx, rcx, rdx
       je       , G_M000_IG14
 0xD1FFAB1E
       call     
G_M000_IG06:CORINFO_HELP_COUNTPROFILE32

       jmp            SHORT G_M000_IG29
  add      
G_M000_IG27:eax
, 1
       jo       G_M000_IG22
             mov      rdx, gword ptr [rbp+0x10]
 cmp      eax, edi
             mov      rcx, 0xD1FFAB1E
 mov             call     [CORINFO_HELP_ISINSTANCEOFCLASS]
r10       test     rax, rax
       jne      G_M000_IG33
, rax
             mov      rdx, gword ptr [rbp+0x10] jge      G_M000_IG17
 

G_M000_IG07:
       mov            rcx mov      rax, gword ptr [rsp+0x20]
 , 0xD1FFAB1E
G_M000_IG08:

       call     [      CORINFO_HELP_ISINSTANCEOFCLASS]
 test     r12, r12
       jne      G_M000_IG19
 
G_M000_IG09:
       test     rax, rax
       jne      SHORT G_M000_IG32
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E      
 cmp            r10d call     , r13d[CORINFO_HELP_ISINSTANCEOFCLASS]

             mov      gword ptr [ jge      G_M000_IG13
 rbp-0x48], rax

G_M000_IG10:
             cmp      gword ptr [rbp-0x48], 0
       je        cmp      r10d, dword ptr SHORT G_M000_IG31
[rsi      +0x08]
       mov      rcx, gword ptr [rbp-0x48]
 jae      G_M000_IG21      
 cmp            dword ptr  imul     rdx, [rcx]r10, ecx, 48

       vmovdqu  ymm0, ymmword ptr [rsi+rdx+0x10]
             call      vmovdqu  ymmword ptr [rsp+0x38], ymm0
       vmovdqu  xmm0, xmmword ptr [rsi+rdx+0x30]
       vmovdqu  xmmword ptr [rsp+0x58], xmm0
 [OoplesFinance.StockIndicators.Indicators.DecayingWindowExtreme:get_Decay():double:this]

G_M000_IG11:
             vmovdqu  ymm0, ymmword ptr  vxorps   xmm1, xmm1, [rsp+0x38]xmm1

       vmovdqu        ymmword ptr [rsp+0x188], ymm0
 vucomisd xmm0, xmm1
             vmovdqu   jp       SHORT G_M000_IG30
       jne      xmm0SHORT G_M000_IG30
 , xmmword ptr [rsp+0x58]
       vmovdqu  
G_M000_IG28:
xmmword ptr [rsp+0x1A8], xmm0
       mov      rcx      , 0xD1FFAB1E
 vmovsd         xmm0, qword ptr [rsp+0x190]
 call            vmovsd   xmm1, qword ptr [rsp+0x198]
CORINFO_HELP_COUNTPROFILE32      
 
G_M000_IG29:
 vmovsd   xmm2, qword ptr [rsp+0x1A0]
       mov      dword ptr [rbp-0x4C], 1
             jmp       vbroadcastsdSHORT G_M000_IG35
  
G_M000_IG30:
       mov      rcx, 0xD1FFAB1E
ymm3, qword ptr [reloc @RWD00]
       vunpcklpd xmm0, xmm0, xmm1
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG34
        vmovsd   xmm1, qword ptr [rsp+0x1A8
G_M000_IG31:
       mov      rcx, 0xD1FFAB1E
]
       call     CORINFO_HELP_COUNTPROFILE32
             jmp       vunpcklpd xmm1, xmm2, xmm1
SHORT       G_M000_IG34
  vinsertf128
G_M000_IG32: 
ymm0, ymm0, xmm1
       mov      rcx, 0xD1FFAB1E      
 vpand    ymm0      , ymm0, ymm3
 call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG29      
  vpcmpeqq ymm0, ymm0, ymm3

G_M000_IG33:
             mov      rcx, 0xD1FFAB1E
 vptest   ymm0, ymm0      
 call     CORINFO_HELP_COUNTPROFILE32
             jne      G_M000_IG16
 jmp      SHORT G_M000_IG29
       
G_M000_IG34:
       mov      rcx, 0xD1FFAB1E mov      rdx
, qword ptr [rsp+0x1B0]
             call     CORINFO_HELP_COUNTPROFILE32
       xor      eax, eax
 mov             mov      dword ptr rcx[rbp-0x4C], eax
 , 0xD1FFAB1E

G_M000_IG35:
             andn     rdx cmp      dword ptr [rbp-0x4C], 0
, rdx, rcx
             jne      G_M000_IG43 mov      qword ptr 
[rsp+0x28], r10
       je       G_M000_IG16
        mov      rcx
G_M000_IG12:
       lea      rdx, [, gword ptr [rbp+0x10]
rsp      +0x188]
 call            mov      rcx, rax
       cmp      dword ptr [rcx], ecx
[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:PivotReplayWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG41
       call            mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.MatchedLinesState:Update(byref):double:this]
       mov      rax, qword ptr [rsp+0x28]
       cmp      eax, dword ptr [r15+0x08]
       jae      G_M000_IG21
[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:RickshawReplayWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]       vmovsd   
qword ptr [r15+8*rax+0x10], xmm0
       test            test     rbx, rbx
       je       G_M000_IG05
       jmp      G_M000_IG03
 eax, eax
       jg       G_M000_IG40
       mov      rcx, gword ptr [
G_M000_IG13:
rbp+0x10]
       mov             call     qword ptr [rsp+0x28], r10
[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:IsRollingDiagnostics(OoplesFinance.StockIndicators.Indicators.IIndicator):bool]
       mov             test     edx, r10d
eax, eax
       jne      G_M000_IG39
       sub      edx, r13d
       jo       G_M000_IG22
       mov      rcx       cmp      edx, dword ptr [rbp+, gword ptr [rbp+0x10]
       call     0x08]
       jae      G_M000_IG21
[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:AverageOscillatorWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       mov      rcx, bword ptr [rbp]       test     
eax       imul     rdx, rdx, , eax
       jg       G_M000_IG38
       mov      rcx, gword ptr [rbp48
+0x10]
       vmovdqu  ymm0, ymmword ptr [       call     rcx+rdx]
       vmovdqu  ymmword ptr [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:TradyStochasticWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
[rsp+0x38], ymm0       test     
       vmovdqu  xmm0, xmmword ptr [rcx+rdx+0x20]
eax, eax
       vmovdqu  xmmword ptr [rsp+0x58], xmm0
       jg       SHORT G_M000_IG37
       mov      rcx, gword ptr [rbp       mov      r10, qword ptr [rsp+0x28]+0x10]

       call            jmp      G_M000_IG11
 [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:SmoothedStochasticWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]

G_M000_IG14:
             mov      r8d, dword ptr [ test     eax, eax
rsp+0x34]       jg       SHORT G_M000_IG36
       mov      rdx, gword ptr [rbp
+0x10]
       test            mov      rcx, 0xD1FFAB1Er8d
, r8d
       call     [       mov      CORINFO_HELP_ISINSTANCEOFCLASS]
dword ptr        mov      gword ptr [rbp-0x60], rax
       cmp      gword ptr [rbp-0x60], 0
[rsp+0x34]       je       G_M000_IG42
       mov      , r8d
rcx, 0xD1FFAB1E
             call     CORINFO_HELP_COUNTPROFILE32
 jge      G_M000_IG06
        mov      rcx, gword ptr [rbp-0x60]

G_M000_IG15:
       cmp             mov      r8d, eax
dword ptr [rcx]       vmovsd   qword ptr [rsp+0x210], xmm0
, ecx
       call            mov      dword ptr [rsp+0x34][OoplesFinance.StockIndicators.Indicators.WindowReturnBeta:get_Period():int:this]
, r8d
       cmp             jmp      G_M000_IG06
 eax, 0xD1FFAB1E

G_M000_IG16:
       setl            mov      byte  ptr [rsp+0x208], 1
 al
       movzx    
G_M000_IG17:
rax, al
       vmovdqu  ymm0, ymmword ptr [       mov      dword ptr [rbprsp+0x188]-0x94], eax

       vmovdqu         jmp      G_M000_IG44
 ymmword ptr [rsp+0x1D8]
G_M000_IG36:
      , ymm0
 mov      rcx,        vmovdqu  xmm0, xmmword ptr [rsp+0x1A8]
0xD1FFAB1E
       vmovdqu  xmmword ptr [rsp+0x1F8], xmm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x1D8]
       call     CORINFO_HELP_COUNTPROFILE32
       vmovdqu         jmp      SHORT G_M000_IG43
 ymmword ptr [r14]
G_M000_IG37:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG43
 , ymm0

G_M000_IG38:
       mov      rcx, 0xD1FFAB1E
       vmovdqu  ymm0, ymmword ptr [rsp+0x1F8]
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG43
        vmovdqu  ymmword ptr [
G_M000_IG39:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
r14       jmp      +0x20]SHORT G_M000_IG43
 , ymm0

G_M000_IG40:
             mov       mov      rcx, 0xD1FFAB1Erax
, qword ptr [rsp+0x218]
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG43
        mov      
G_M000_IG41:
       mov      rcx, 0xD1FFAB1E
qword ptr [r14+0x40], rax
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG43
        mov      
G_M000_IG42:
       mov      rcx, 0xD1FFAB1E
r8d, dword ptr [rsp+0x34]       call     CORINFO_HELP_COUNTPROFILE32

       xor             mov      dword ptr [r14+0x34], r8d
eax       mov      rax, r14
, eax
        mov      dword ptr [rbp-0x94]
G_M000_IG18:, eax
       jmp      SHORT G_M000_IG44
 

G_M000_IG43:
       vzeroupper 
       mov      dword ptr [rbp-0x94], 1
        add      rsp, 552

G_M000_IG44:
             movzx    rax, byte  ptr [rbp-0x94]
 pop      rbx
       pop      rsi       mov      dword ptr [rbp-0x50], eax

       pop      rdi       cmp      dword ptr [rbp-0x50], 0
       jne      G_M000_IG56

       pop      r12
       pop      r13
             mov      rdx, gword ptr [rbp+0x10]
 pop      r14
       pop      r15
       pop      rbp
       mov      rcx, 0xD1FFAB1E      
 ret             call     
 [CORINFO_HELP_ISINSTANCEOFCLASS]

G_M000_IG19:
       mov      gword ptr [rbp-0x68], rax
       cmp      gword ptr [rbp-0x68], 0       cmp      dword ptr [r12+0x20], 0

       jne      SHORT G_M000_IG17
       jmp      G_M000_IG09
        je       SHORT G_M000_IG45

G_M000_IG20:
       mov      rcx, gword ptr [rbp-0x68]
       mov      rcx, gword ptr [rbx+0x10]
       cmp      dword ptr        cmp      eax, dword ptr [rcx+0x08]
       jae      SHORT G_M000_IG21
[       imul     rcx]rdx, rax, 48
, ecx
       call            lea      rcx, bword ptr [rcx+rdx+0x10]
       jmp      G_M000_IG04
 
G_M000_IG21:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 [OoplesFinance.StockIndicators.Indicators.WindowMoneyFlowIndex:get_Period():int:this]

G_M000_IG22:
       cmp             call     CORINFO_HELP_OVERFLOW
       int3     
 eax, 0xD1FFAB1E
       jl       G_M000_IG51
       jmp      
G_M000_IG54
 RWD00  
G_M000_IG45:
	dq	7FF0000000000000h
       mov      rdx
; Total bytes of code 844

, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x70], rax
       cmp      gword ptr [rbp-0x70], 0
       je       SHORT G_M000_IG47
       mov      rcx, gword ptr [rbp-0x70]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.WindowVortex:get_Period():int:this]
       cmp      eax, 0xD1FFAB1E
       jl       SHORT G_M000_IG46
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG54
 
G_M000_IG46:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG51
 
G_M000_IG47:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x78], rax
       cmp      gword ptr [rbp-0x78], 0
       je       SHORT G_M000_IG49
       mov      rcx, gword ptr [rbp-0x78]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.WindowUltimateOscillator:get_LongPeriod():int:this]
       cmp      eax, 0xD1FFAB1E
       jl       SHORT G_M000_IG48
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG54
 
G_M000_IG48:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG51
 
G_M000_IG49:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x80], rax
       cmp      gword ptr [rbp-0x80], 0
       je       SHORT G_M000_IG53
       mov      rcx, gword ptr [rbp-0x80]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.WindowChoppinessIndex:get_Period():int:this]
       cmp      eax, 0xD1FFAB1E
       jge      SHORT G_M000_IG52
 
G_M000_IG50:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG51:
       mov      dword ptr [rbp-0x84], 1
       jmp      SHORT G_M000_IG55
 
G_M000_IG52:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG54
 
G_M000_IG53:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG54:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       xor      eax, eax
       mov      dword ptr [rbp-0x84], eax
 
G_M000_IG55:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       mov      eax, dword ptr [rbp-0x84]
       mov      dword ptr [rbp-0x50], eax
 
G_M000_IG56:
       mov      eax, dword ptr [rbp-0x50]
       mov      dword ptr [rbp-0x54], eax
       cmp      dword ptr [rbp-0x54], 0
       jne      G_M000_IG62
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       test     rax, rax
       jne      SHORT G_M000_IG58
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       test     rax, rax
       jne      SHORT G_M000_IG59
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       test     rax, rax
       je       SHORT G_M000_IG60
 
G_M000_IG57:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG58:
       mov      dword ptr [rbp-0x84], 1
       jmp      SHORT G_M000_IG61
 
G_M000_IG59:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG58
 
G_M000_IG60:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       xor      eax, eax
       mov      dword ptr [rbp-0x84], eax
 
G_M000_IG61:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       mov      eax, dword ptr [rbp-0x84]
       mov      dword ptr [rbp-0x54], eax
 
G_M000_IG62:
       cmp      dword ptr [rbp-0x54], 0
       jne      G_M000_IG99
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:CommodityReplayWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG98
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:BodyShadowPeriod(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       mov      dword ptr [rbp-0x88], eax
       cmp      dword ptr [rbp-0x88], 0
       jle      SHORT G_M000_IG64
       cmp      dword ptr [rbp-0x88], 0xD1FFAB1E
       jl       G_M000_IG97
 
G_M000_IG63:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG64:
       mov      rdx, gword ptr [rbp+0x10]
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_ISINSTANCEOFCLASS]
       mov      gword ptr [rbp-0x90], rax
       cmp      gword ptr [rbp-0x90], 0
       je       SHORT G_M000_IG66
       mov      rcx, gword ptr [rbp-0x90]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.RollingPercentile:get_HasFractionalFullRank():bool:this]
       test     eax, eax
       jne      G_M000_IG96
 
G_M000_IG65:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG66:
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:RelativeVolatilityWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG95
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:HullReplayWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG94
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:DetailedDeviationWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG93
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:AroonWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG92
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:WeightedWindowPeriod(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG91
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:PercentileCandlePeriod(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG90
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:HaramiWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG89
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:TasukiWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG88
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:TrendReversalWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG87
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ContainmentWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG86
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:NeckWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG85
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:KickingWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
 
G_M000_IG67:
       jg       G_M000_IG84
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:MatchedLinesWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG83
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:SequenceWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG82
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ContextReversalWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG81
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:TripleBodyWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG80
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:PenetrationWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG79
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:StarReversalWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG78
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:GapContinuationWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG77
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:CrowSoldierWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG76
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ExhaustionWindowLength(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG75
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:FiveContinuationWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG74
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ExtendedReversalWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       G_M000_IG73
       mov      rcx, gword ptr [rbp+0x10]
 
G_M000_IG68:
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:HikkakeWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       SHORT G_M000_IG72
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:DelayedCloudWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       jg       SHORT G_M000_IG71
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:IsRawDirectional(OoplesFinance.StockIndicators.Indicators.IIndicator):bool]
       test     eax, eax
       jne      SHORT G_M000_IG70
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       mov      rcx, gword ptr [rbp+0x10]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:RangeDojiWindow(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       test     eax, eax
       setg     al
       movzx    rax, al
 
G_M000_IG69:
       add      rsp, 192
       pop      rbp
       ret      
 
G_M000_IG70:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG71:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG72:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG73:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG74:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG75:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG76:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG77:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG78:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG79:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG80:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG81:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG82:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG83:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG84:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG85:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG86:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG87:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG88:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG89:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG90:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      G_M000_IG99
 
G_M000_IG91:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG92:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG93:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG94:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG95:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG96:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG97:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       jmp      SHORT G_M000_IG99
 
G_M000_IG98:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
 
G_M000_IG99:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_COUNTPROFILE32
       mov      eax, 1
 
G_M000_IG100:
       add      rsp, 192
       pop      rbp
       ret      
 
; Total bytes of code 3613

