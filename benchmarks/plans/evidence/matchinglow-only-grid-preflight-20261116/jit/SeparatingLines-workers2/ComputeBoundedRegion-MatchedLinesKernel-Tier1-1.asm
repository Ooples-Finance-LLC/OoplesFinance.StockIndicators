; Assembly listing for method OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ComputeBoundedRegion[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+MatchedLinesKernel](OoplesFinance.StockIndicators.Indicators.Bar[],double[][],OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer,int,int,int,System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.IIndicator):OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+PointwiseRegion (Tier1-OSR)
; Emitting BLENDED_CODE for generic X64 + VEX on Windows
; Tier1-OSR code
; OSR variant for entry point 0x3ad
; optimized code
; optimized using Synthesized PGO
; rsp based frame
; partially interruptible
; with Synthesized PGO: fgCalledCount is 18
; 16 inlinees with PGO data; 44 single block inlinees; 0 inlinees without PGO data

G_M000_IG01:
       sub      rsp, 168
       mov      qword ptr [rsp+0x258], r15
       mov      qword ptr [rsp+0x250], r14
       mov      qword ptr [rsp+0x248], r13
       mov      qword ptr [rsp+0x240], r12
       mov      qword ptr [rsp+0x238], rdi
       mov      qword ptr [rsp+0x230], rsi
       mov      qword ptr [rsp+0x228], rbx
       mov      r14, qword ptr [rsp+0x270]
       mov      rsi, gword ptr [rsp+0x278]
       mov      rbx, gword ptr [rsp+0x288]
       mov      edi, dword ptr [rsp+0x298]
       mov      rbp, bword ptr [rsp+0x2B0]
       mov      r15, gword ptr [rsp+0x1B8]
       mov      r13d, dword ptr [rsp+0x184]
       mov      ecx, dword ptr [rsp+0x11C]
 
G_M000_IG02:
       mov      r12, gword ptr [rsp+0x2B8]
       mov      rax, gword ptr [rsp+0x1C0]
       mov      gword ptr [rsp+0x20], rax
       mov      r8d, dword ptr [rsp+0x20C]
       mov      dword ptr [rsp+0x34], r8d
       mov      r10d, ecx
       cmp      r10d, edi
       jl       G_M000_IG08
       jmp      G_M000_IG17
 
G_M000_IG03:
       cmp      gword ptr [rbx+0x10], 0
       jne      G_M000_IG20
       mov      rcx, gword ptr [rbx+0x08]
       mov      edx, eax
       sar      edx, 10
       cmp      edx, dword ptr [rcx+0x08]
       jae      G_M000_IG21
       mov      rcx, gword ptr [rcx+8*rdx+0x10]
       mov      edx, eax
       and      edx, 0x3FF
       cmp      edx, dword ptr [rcx+0x08]
       jae      G_M000_IG21
       imul     rdx, rdx, 48
       lea      rcx, bword ptr [rcx+rdx+0x10]
 
G_M000_IG04:
       vmovdqu  ymm1, ymmword ptr [rsp+0x188]
       vmovdqu  ymmword ptr [rcx], ymm1
       vmovdqu  xmm1, xmmword ptr [rsp+0x1A8]
       vmovdqu  xmmword ptr [rcx+0x20], xmm1
 
G_M000_IG05:
       vmovq    rcx, xmm0
       mov      rdx, 0xD1FFAB1E
       andn     rcx, rcx, rdx
       je       G_M000_IG14
 
G_M000_IG06:
       add      eax, 1
       jo       G_M000_IG22
       cmp      eax, edi
       mov      r10, rax
       jge      G_M000_IG17
 
G_M000_IG07:
       mov      rax, gword ptr [rsp+0x20]
 
G_M000_IG08:
       test     r12, r12
       jne      G_M000_IG19
 
G_M000_IG09:
       cmp      r10d, r13d
       jge      G_M000_IG13
 
G_M000_IG10:
       cmp      r10d, dword ptr [rsi+0x08]
       jae      G_M000_IG21
       imul     rdx, r10, 48
       vmovdqu  ymm0, ymmword ptr [rsi+rdx+0x10]
       vmovdqu  ymmword ptr [rsp+0x38], ymm0
       vmovdqu  xmm0, xmmword ptr [rsi+rdx+0x30]
       vmovdqu  xmmword ptr [rsp+0x58], xmm0
 
G_M000_IG11:
       vmovdqu  ymm0, ymmword ptr [rsp+0x38]
       vmovdqu  ymmword ptr [rsp+0x188], ymm0
       vmovdqu  xmm0, xmmword ptr [rsp+0x58]
       vmovdqu  xmmword ptr [rsp+0x1A8], xmm0
       vmovsd   xmm0, qword ptr [rsp+0x190]
       vmovsd   xmm1, qword ptr [rsp+0x198]
       vmovsd   xmm2, qword ptr [rsp+0x1A0]
       vbroadcastsd ymm3, qword ptr [reloc @RWD00]
       vunpcklpd xmm0, xmm0, xmm1
       vmovsd   xmm1, qword ptr [rsp+0x1A8]
       vunpcklpd xmm1, xmm2, xmm1
       vinsertf128 ymm0, ymm0, xmm1
       vpand    ymm0, ymm0, ymm3
       vpcmpeqq ymm0, ymm0, ymm3
       vptest   ymm0, ymm0
       jne      G_M000_IG16
       mov      rdx, qword ptr [rsp+0x1B0]
       mov      rcx, 0xD1FFAB1E
       andn     rdx, rdx, rcx
       mov      qword ptr [rsp+0x28], r10
       je       G_M000_IG16
 
G_M000_IG12:
       lea      rdx, [rsp+0x188]
       mov      rcx, rax
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.MatchedLinesState:Update(byref):double:this]
       mov      rax, qword ptr [rsp+0x28]
       cmp      eax, dword ptr [r15+0x08]
       jae      G_M000_IG21
       vmovsd   qword ptr [r15+8*rax+0x10], xmm0
       test     rbx, rbx
       je       G_M000_IG05
       jmp      G_M000_IG03
 
G_M000_IG13:
       mov      qword ptr [rsp+0x28], r10
       mov      edx, r10d
       sub      edx, r13d
       jo       G_M000_IG22
       cmp      edx, dword ptr [rbp+0x08]
       jae      G_M000_IG21
       mov      rcx, bword ptr [rbp]
       imul     rdx, rdx, 48
       vmovdqu  ymm0, ymmword ptr [rcx+rdx]
       vmovdqu  ymmword ptr [rsp+0x38], ymm0
       vmovdqu  xmm0, xmmword ptr [rcx+rdx+0x20]
       vmovdqu  xmmword ptr [rsp+0x58], xmm0
       mov      r10, qword ptr [rsp+0x28]
       jmp      G_M000_IG11
 
G_M000_IG14:
       mov      r8d, dword ptr [rsp+0x34]
       test     r8d, r8d
       mov      dword ptr [rsp+0x34], r8d
       jge      G_M000_IG06
 
G_M000_IG15:
       mov      r8d, eax
       vmovsd   qword ptr [rsp+0x210], xmm0
       mov      dword ptr [rsp+0x34], r8d
       jmp      G_M000_IG06
 
G_M000_IG16:
       mov      byte  ptr [rsp+0x208], 1
 
G_M000_IG17:
       vmovdqu  ymm0, ymmword ptr [rsp+0x188]
       vmovdqu  ymmword ptr [rsp+0x1D8], ymm0
       vmovdqu  xmm0, xmmword ptr [rsp+0x1A8]
       vmovdqu  xmmword ptr [rsp+0x1F8], xmm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x1D8]
       vmovdqu  ymmword ptr [r14], ymm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x1F8]
       vmovdqu  ymmword ptr [r14+0x20], ymm0
       mov      rax, qword ptr [rsp+0x218]
       mov      qword ptr [r14+0x40], rax
       mov      r8d, dword ptr [rsp+0x34]
       mov      dword ptr [r14+0x34], r8d
       mov      rax, r14
 
G_M000_IG18:
       vzeroupper 
       add      rsp, 552
       pop      rbx
       pop      rsi
       pop      rdi
       pop      r12
       pop      r13
       pop      r14
       pop      r15
       pop      rbp
       ret      
 
G_M000_IG19:
       cmp      dword ptr [r12+0x20], 0
       jne      SHORT G_M000_IG17
       jmp      G_M000_IG09
 
G_M000_IG20:
       mov      rcx, gword ptr [rbx+0x10]
       cmp      eax, dword ptr [rcx+0x08]
       jae      SHORT G_M000_IG21
       imul     rdx, rax, 48
       lea      rcx, bword ptr [rcx+rdx+0x10]
       jmp      G_M000_IG04
 
G_M000_IG21:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
G_M000_IG22:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
RWD00  	dq	7FF0000000000000h

; Total bytes of code 844

