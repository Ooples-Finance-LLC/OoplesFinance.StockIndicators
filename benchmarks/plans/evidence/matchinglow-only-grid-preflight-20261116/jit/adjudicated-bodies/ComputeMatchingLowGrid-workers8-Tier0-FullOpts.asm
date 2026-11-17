; Assembly listing for method OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ComputeMatchingLowGrid(OoplesFinance.StockIndicators.Indicators.Bar[],double[],OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer,int,int,System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.MatchingLowCandle):OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+PointwiseRegion (Tier0-FullOpts)
; Emitting BLENDED_CODE for generic X64 + VEX on Windows
; Tier-0 switched to FullOpts code
; optimized code
; optimized using Synthesized PGO
; rbp based frame
; fully interruptible
; with Synthesized PGO: fgCalledCount is 100
; No PGO data
; 5 inlinees with PGO data; 22 single block inlinees; 12 inlinees without PGO data

G_M000_IG01:
       push     rbp
       push     r15
       push     r14
       push     r13
       push     r12
       push     rdi
       push     rsi
       push     rbx
       sub      rsp, 552
       vmovaps  xmmword ptr [rsp+0x210], xmm6
       vmovaps  xmmword ptr [rsp+0x200], xmm7
       vmovaps  xmmword ptr [rsp+0x1F0], xmm8
       vmovaps  xmmword ptr [rsp+0x1E0], xmm9
       vmovaps  xmmword ptr [rsp+0x1D0], xmm10
       lea      rbp, [rsp+0x30]
       xor      eax, eax
       mov      qword ptr [rbp+0x78], rax
       vxorps   xmm4, xmm4, xmm4
       vmovdqu  ymmword ptr [rbp+0x80], ymm4
       vmovdqu  ymmword ptr [rbp+0xA0], ymm4
       vmovdqu  ymmword ptr [rbp+0xC0], ymm4
       vmovdqa  xmmword ptr [rbp+0xE0], xmm4
       mov      qword ptr [rbp+0xF0], rax
       mov      rax, 0xD1FFAB1E
       mov      qword ptr [rbp+0x18], rax
       mov      rbx, rcx
       mov      rsi, rdx
       mov      r14, r8
       mov      rdi, r9
       mov      rcx, bword ptr [rbp+0x270]
       mov      rdx, bword ptr [rbp+0x278]
       mov      r15, gword ptr [rbp+0x288]
 
G_M000_IG02:
       mov      r13, bword ptr [rdx]
       mov      r12d, dword ptr [rdx+0x08]
       mov      rax, bword ptr [rcx]
       mov      bword ptr [rbp+0x50], rax
       mov      r8d, dword ptr [rcx+0x08]
       mov      dword ptr [rbp+0xB8], r8d
       mov      r10d, dword ptr [r15+0x20]
       add      r10d, 1
       jo       G_M000_IG56
       mov      dword ptr [rbp+0x19C], r10d
       mov      rcx, -0x3C30
       add      rcx, rsp
       jb       SHORT G_M000_IG03
       xor      ecx, ecx
 
G_M000_IG03:
       test     dword ptr [rsp], esp
       sub      rsp, 0x1000
       cmp      rsp, rcx
       jae      SHORT G_M000_IG03
       mov      rsp, rcx
       lea      rcx, [rsp+0x30]
       mov      qword ptr [rbp+0x68], rcx
       mov      edx, 0x3C30
       call     [CORINFO_HELP_MEMZERO]
       mov      rcx, qword ptr [rbp+0x68]
       mov      rax, rcx
       mov      bword ptr [rbp+0x48], rax
       mov      rcx, -0x1E30
       add      rcx, rsp
       jb       SHORT G_M000_IG04
       xor      ecx, ecx
 
G_M000_IG04:
       test     dword ptr [rsp], esp
       sub      rsp, 0x1000
       cmp      rsp, rcx
       jae      SHORT G_M000_IG04
       mov      rsp, rcx
       lea      rcx, [rsp+0x30]
       mov      qword ptr [rbp+0x60], rcx
       mov      edx, 0x1E30
       call     [CORINFO_HELP_MEMZERO]
       mov      rcx, qword ptr [rbp+0x60]
       mov      rax, rcx
       mov      bword ptr [rbp+0x40], rax
       vxorps   ymm0, ymm0, ymm0
       vmovdqu  ymmword ptr [rbp+0x150], ymm0
       vmovdqu  ymmword ptr [rbp+0x170], ymm0
       vmovdqu  xmmword ptr [rbp+0x188], xmm0
       vxorps   xmm6, xmm6, xmm6
       vxorps   xmm7, xmm7, xmm7
       vxorps   xmm8, xmm8, xmm8
       vxorps   xmm9, xmm9, xmm9
       vxorps   xmm10, xmm10, xmm10
       xor      r10d, r10d
       mov      dword ptr [rbp+0xA8], r10d
       mov      r9d, dword ptr [rbp+0xB8]
       cmp      r9d, 321
       jg       G_M000_IG54
       mov      r8d, r9d
       imul     r8, r8, 48
       mov      rcx, bword ptr [rbp+0x48]
       mov      rdx, bword ptr [rbp+0x50]
       call     [System.SpanHelpers:Memmove(byref,byref,nuint)]
       mov      rcx, bword ptr [rbp+0x50]
       xor      r9d, r9d
       mov      eax, dword ptr [rbp+0xB8]
       test     eax, eax
       jle      SHORT G_M000_IG09
 
G_M000_IG05:
       vbroadcastsd ymm0, qword ptr [reloc @RWD00]
 
G_M000_IG06:
       imul     rdx, r9, 48
       add      rdx, rcx
       vmovsd   xmm1, qword ptr [rdx+0x08]
       vmovsd   xmm2, qword ptr [rdx+0x10]
       vmovsd   xmm3, qword ptr [rdx+0x18]
       vmovaps  ymm4, ymm0
       vunpcklpd xmm1, xmm1, xmm2
       vmovsd   xmm2, qword ptr [rdx+0x20]
       vunpcklpd xmm2, xmm3, xmm2
       vinsertf128 ymm1, ymm1, xmm2
       vpand    ymm1, ymm1, ymm4
       vpcmpeqq ymm1, ymm1, ymm4
       vptest   ymm1, ymm1
       jne      G_M000_IG36
 
G_M000_IG07:
       mov      r8, qword ptr [rdx+0x28]
       mov      r10, 0xD1FFAB1E
       andn     r8, r8, r10
       je       G_M000_IG36
 
G_M000_IG08:
       inc      r9d
       cmp      r9d, eax
       jl       SHORT G_M000_IG06
 
G_M000_IG09:
       mov      r10d, dword ptr [rbp+0x268]
       mov      r11d, r10d
       sub      r11d, r12d
       jo       G_M000_IG56
       mov      dword ptr [rbp+0x148], r11d
       mov      r8d, dword ptr [rbp+0x260]
       cmp      r8d, r10d
       jge      G_M000_IG51
 
G_M000_IG10:
       mov      rdx, gword ptr [rbp+0x280]
       test     rdx, rdx
       je       SHORT G_M000_IG12
 
G_M000_IG11:
       cmp      dword ptr [rdx+0x20], 0
       jne      G_M000_IG51
 
G_M000_IG12:
       mov      dword ptr [rbp+0x268], r10d
       mov      r9d, r10d
       sub      r9d, r8d
       jo       G_M000_IG56
       cmp      r9d, 256
       jl       G_M000_IG40
 
G_M000_IG13:
       mov      ecx, 256
 
G_M000_IG14:
       mov      r9d, ecx
       mov      dword ptr [rbp+0xF8], r9d
       xor      ecx, ecx
       cmp      ecx, r9d
       jge      G_M000_IG26
 
G_M000_IG15:
       test     rdx, rdx
       je       SHORT G_M000_IG17
 
G_M000_IG16:
       mov      gword ptr [rbp+0x280], rdx
       cmp      dword ptr [rdx+0x20], 0
       mov      rdx, gword ptr [rbp+0x280]
       jne      G_M000_IG51
 
G_M000_IG17:
       mov      r10d, r8d
       add      r10d, ecx
       jo       G_M000_IG56
       cmp      r10d, r11d
       jge      G_M000_IG41
 
G_M000_IG18:
       mov      gword ptr [rbp+0x280], rdx
       cmp      r10d, dword ptr [rsi+0x08]
       jae      G_M000_IG55
       mov      edx, r10d
       imul     rdx, rdx, 48
       lea      rdx, bword ptr [rsi+rdx+0x10]
       vmovsd   xmm6, qword ptr [rdx+0x08]
       vmovsd   xmm7, qword ptr [rdx+0x10]
       vmovsd   xmm8, qword ptr [rdx+0x18]
       vmovsd   xmm9, qword ptr [rdx+0x20]
       vmovsd   xmm10, qword ptr [rdx+0x28]
       mov      rdx, qword ptr [rdx]
       mov      qword ptr [rbp+0xC0], rdx
 
G_M000_IG19:
       mov      rdx, qword ptr [rbp+0xC0]
       mov      qword ptr [rbp+0x150], rdx
       vbroadcastsd ymm0, qword ptr [reloc @RWD00]
       vmovaps  xmm1, xmm6
       vmovaps  xmm2, xmm7
       vunpcklpd xmm1, xmm1, xmm2
       vmovaps  xmm2, xmm8
       vmovaps  xmm3, xmm9
       vunpcklpd xmm2, xmm2, xmm3
       vinsertf128 ymm1, ymm1, xmm2
       vpand    ymm1, ymm1, ymm0
       vpcmpeqq ymm0, ymm1, ymm0
       vptest   ymm0, ymm0
       jne      G_M000_IG50
 
G_M000_IG20:
       vmovq    r11, xmm10
       mov      qword ptr [rbp+0x10], r11
       mov      r11, 0xD1FFAB1E
       mov      qword ptr [rbp+0x08], r11
       mov      r11, qword ptr [rbp+0x10]
       andn     r11, r11, qword ptr [rbp+0x08]
       je       G_M000_IG50
 
G_M000_IG21:
       mov      dword ptr [rbp+0x14C], eax
       mov      r11d, eax
       add      r11d, ecx
       jo       G_M000_IG56
       cmp      r11d, 321
       jae      G_M000_IG55
       imul     r11, r11, 48
       mov      rax, bword ptr [rbp+0x48]
       add      r11, rax
       vmovsd   qword ptr [r11+0x08], xmm6
       vmovsd   qword ptr [r11+0x10], xmm7
       vmovsd   qword ptr [r11+0x18], xmm8
       vmovsd   qword ptr [r11+0x20], xmm9
       vmovsd   qword ptr [r11+0x28], xmm10
       mov      qword ptr [r11], rdx
       test     rdi, rdi
       je       SHORT G_M000_IG25
 
G_M000_IG22:
       cmp      gword ptr [rdi+0x10], 0
       jne      G_M000_IG42
 
G_M000_IG23:
       mov      r11, gword ptr [rdi+0x08]
       mov      gword ptr [rbp+0x20], r11
       mov      r11d, r10d
       sar      r11d, 10
       mov      r9, gword ptr [rbp+0x20]
       cmp      r11d, dword ptr [r9+0x08]
       jae      G_M000_IG55
       mov      r11d, r10d
       sar      r11d, 10
       mov      r9, gword ptr [r9+8*r11+0x10]
       mov      r11d, r10d
       and      r11d, 0x3FF
       cmp      r11d, dword ptr [r9+0x08]
       jae      G_M000_IG55
       and      r10d, 0x3FF
       imul     r10, r10, 48
       lea      r9, bword ptr [r9+r10+0x10]
 
G_M000_IG24:
       vmovsd   qword ptr [r9+0x08], xmm6
       vmovsd   qword ptr [r9+0x10], xmm7
       vmovsd   qword ptr [r9+0x18], xmm8
       vmovsd   qword ptr [r9+0x20], xmm9
       vmovsd   qword ptr [r9+0x28], xmm10
       mov      qword ptr [r9], rdx
 
G_M000_IG25:
       add      ecx, 1
       jo       G_M000_IG56
       mov      r9d, dword ptr [rbp+0xF8]
       cmp      ecx, r9d
       mov      eax, dword ptr [rbp+0x14C]
       mov      rdx, gword ptr [rbp+0x280]
       mov      r11d, dword ptr [rbp+0x148]
       jl       G_M000_IG15
 
G_M000_IG26:
       mov      dword ptr [rbp+0x14C], eax
       mov      r9d, dword ptr [rbp+0xF8]
       mov      ecx, eax
       add      ecx, r9d
       jo       G_M000_IG56
       mov      dword ptr [rbp+0xBC], ecx
       cmp      ecx, 321
       ja       G_M000_IG53
       mov      rax, bword ptr [rbp+0x48]
       mov      bword ptr [rbp+0x38], rax
       mov      ecx, dword ptr [rbp+0xBC]
       mov      dword ptr [rbp+0xB4], ecx
       test     r14, r14
       jne      G_M000_IG43
 
G_M000_IG27:
       mov      r10d, r8d
       or       r10d, r9d
       jne      G_M000_IG53
       xor      r10, r10
       mov      bword ptr [rbp+0x28], r10
       xor      r10d, r10d
       mov      dword ptr [rbp+0xFC], r8d
       mov      gword ptr [rbp+0x280], rdx
       mov      dword ptr [rbp+0xAC], r10d
 
G_M000_IG28:
       mov      r10, bword ptr [rbp+0x28]
       mov      bword ptr [rbp+0x30], r10
       mov      r10d, dword ptr [rbp+0xAC]
       mov      dword ptr [rbp+0xB0], r10d
       mov      r10, bword ptr [rbp+0x38]
       mov      bword ptr [rbp+0x98], r10
       mov      dword ptr [rbp+0xA0], ecx
       mov      r10, bword ptr [rbp+0x40]
       mov      bword ptr [rbp+0x88], r10
       mov      dword ptr [rbp+0x90], 966
       mov      r11, bword ptr [rbp+0x30]
       mov      bword ptr [rbp+0x78], r11
       mov      r11d, dword ptr [rbp+0xB0]
       mov      dword ptr [rbp+0x80], r11d
       lea      r11, [rbp+0xF0]
       mov      qword ptr [rsp+0x28], r11
       lea      rcx, [rbp+0x98]
       lea      r9, [rbp+0x88]
       lea      r11, [rbp+0x78]
       mov      qword ptr [rsp+0x20], r11
       mov      edx, dword ptr [rbp+0x14C]
       mov      r8, r15
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:TryMatchingLowGridBlock(System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],int,OoplesFinance.StockIndicators.Indicators.MatchingLowCandle,System.Span`1[long],System.Span`1[double],byref):bool]
       test     eax, eax
       jne      G_M000_IG45
 
G_M000_IG29:
       mov      rcx, r15
       call     [OoplesFinance.StockIndicators.Indicators.MatchingLowCandle:CreateState():System.Object:this]
       mov      rdx, rax
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_CHKCASTINTERFACE]
       mov      gword ptr [rbp+0x58], rax
       xor      r8d, r8d
       cmp      dword ptr [rbp+0xB4], 0
       jle      G_M000_IG52
       jmp      SHORT G_M000_IG32
 
G_M000_IG30:
       add      r8d, 1
       jo       G_M000_IG56
       mov      ecx, dword ptr [rbp+0xB4]
       cmp      r8d, ecx
       mov      dword ptr [rbp+0x14C], eax
       jge      G_M000_IG46
 
G_M000_IG31:
       mov      rax, gword ptr [rbp+0x58]
 
G_M000_IG32:
       mov      r9, gword ptr [rbp+0x280]
       test     r9, r9
       jne      G_M000_IG44
 
G_M000_IG33:
       mov      gword ptr [rbp+0x280], r9
 
G_M000_IG34:
       mov      qword ptr [rbp+0x70], r8
       imul     rdx, r8, 48
       add      rdx, bword ptr [rbp+0x48]
       mov      rcx, rax
       mov      r11, 0xD1FFAB1E
       call     [r11]OoplesFinance.StockIndicators.Indicators.IIndicatorState:Update(byref):double:this
       mov      r8, qword ptr [rbp+0x70]
       mov      eax, dword ptr [rbp+0x14C]
       cmp      r8d, eax
       jl       SHORT G_M000_IG30
 
G_M000_IG35:
       mov      ecx, r8d
       sub      ecx, eax
       jo       G_M000_IG56
       mov      edx, dword ptr [rbp+0xB0]
       cmp      ecx, edx
       jae      G_M000_IG55
       mov      r11, bword ptr [rbp+0x30]
       vmovsd   qword ptr [r11+8*rcx], xmm0
       jmp      G_M000_IG30
 
G_M000_IG36:
       vxorps   ymm0, ymm0, ymm0
       vmovdqu  ymmword ptr [rbp+0x100], ymm0
       vmovdqu  ymmword ptr [rbp+0x120], ymm0
       vmovdqu  xmmword ptr [rbp+0x138], xmm0
       vmovsd   xmm0, qword ptr [rdx+0x08]
       vmovsd   xmm1, qword ptr [rdx+0x10]
       vmovsd   xmm2, qword ptr [rdx+0x18]
       vmovsd   xmm3, qword ptr [rdx+0x20]
       vmovsd   xmm4, qword ptr [rdx+0x28]
       mov      rax, qword ptr [rdx]
       mov      qword ptr [rbp+0x100], rax
       vmovdqu  ymm5, ymmword ptr [rbp+0x100]
       vmovdqu  ymmword ptr [rbx], ymm5
       vmovdqu  ymm5, ymmword ptr [rbp+0x120]
       vmovdqu  ymmword ptr [rbx+0x20], ymm5
       mov      rcx, qword ptr [rbp+0x140]
       mov      qword ptr [rbx+0x40], rcx
       vmovsd   qword ptr [rbx+0x08], xmm0
       vmovsd   qword ptr [rbx+0x10], xmm1
       vmovsd   qword ptr [rbx+0x18], xmm2
       vmovsd   qword ptr [rbx+0x20], xmm3
       vmovsd   qword ptr [rbx+0x28], xmm4
       mov      byte  ptr [rbx+0x30], 1
       mov      dword ptr [rbx+0x34], -1
 
G_M000_IG37:
       mov      rax, rbx
       mov      r8, 0xD1FFAB1E
       cmp      qword ptr [rbp+0x18], r8
       je       SHORT G_M000_IG38
       call     CORINFO_HELP_FAIL_FAST
 
G_M000_IG38:
       nop      
 
G_M000_IG39:
       vzeroupper 
       vmovaps  xmm6, xmmword ptr [rbp+0x1E0]
       vmovaps  xmm7, xmmword ptr [rbp+0x1D0]
       vmovaps  xmm8, xmmword ptr [rbp+0x1C0]
       vmovaps  xmm9, xmmword ptr [rbp+0x1B0]
       vmovaps  xmm10, xmmword ptr [rbp+0x1A0]
       lea      rsp, [rbp+0x1F8]
       pop      rbx
       pop      rsi
       pop      rdi
       pop      r12
       pop      r13
       pop      r14
       pop      r15
       pop      rbp
       ret      
 
G_M000_IG40:
       mov      ecx, r9d
       jmp      G_M000_IG14
 
G_M000_IG41:
       mov      gword ptr [rbp+0x280], rdx
       mov      edx, r10d
       sub      edx, r11d
       jo       G_M000_IG56
       cmp      edx, r12d
       jae      G_M000_IG55
       imul     rdx, rdx, 48
       add      rdx, r13
       vmovsd   xmm6, qword ptr [rdx+0x08]
       vmovsd   xmm7, qword ptr [rdx+0x10]
       vmovsd   xmm8, qword ptr [rdx+0x18]
       vmovsd   xmm9, qword ptr [rdx+0x20]
       vmovsd   xmm10, qword ptr [rdx+0x28]
       mov      rdx, qword ptr [rdx]
       mov      qword ptr [rbp+0xC0], rdx
       mov      r11d, dword ptr [rbp+0x148]
       jmp      G_M000_IG19
 
G_M000_IG42:
       mov      r11, gword ptr [rdi+0x10]
       cmp      r10d, dword ptr [r11+0x08]
       jae      G_M000_IG55
       mov      r10d, r10d
       imul     r10, r10, 48
       lea      r10, bword ptr [r11+r10+0x10]
       mov      r9, r10
       jmp      G_M000_IG24
 
G_M000_IG43:
       mov      r10d, r8d
       mov      qword ptr [rbp+0x08], r10
       mov      r10d, r9d
       add      r10, qword ptr [rbp+0x08]
       mov      qword ptr [rbp+0x08], r10
       mov      r10d, dword ptr [r14+0x08]
       cmp      qword ptr [rbp+0x08], r10
       ja       G_M000_IG53
       mov      dword ptr [rbp+0xFC], r8d
       mov      r10d, r8d
       lea      r10, bword ptr [r14+8*r10+0x10]
       mov      bword ptr [rbp+0x28], r10
       mov      dword ptr [rbp+0xAC], r9d
       mov      gword ptr [rbp+0x280], rdx
       jmp      G_M000_IG28
 
G_M000_IG44:
       mov      gword ptr [rbp+0x280], r9
       cmp      dword ptr [r9+0x20], 0
       jne      G_M000_IG51
       jmp      G_M000_IG34
 
G_M000_IG45:
       mov      ecx, dword ptr [rbp+0xB4]
 
G_M000_IG46:
       mov      r8d, dword ptr [rbp+0xFC]
       add      r8d, dword ptr [rbp+0xF8]
       jo       G_M000_IG56
       mov      dword ptr [rbp+0xFC], r8d
       mov      r10d, dword ptr [rbp+0x19C]
       cmp      r10d, ecx
       jg       SHORT G_M000_IG49
 
G_M000_IG47:
       mov      eax, r10d
 
G_M000_IG48:
       mov      dword ptr [rbp+0x14C], eax
       mov      edx, ecx
       sub      edx, eax
       cmp      edx, ecx
       ja       G_M000_IG53
       mov      r9d, edx
       imul     r9, r9, 48
       mov      r11, bword ptr [rbp+0x48]
       add      r9, r11
       sub      ecx, edx
       cmp      ecx, 321
       ja       G_M000_IG54
       imul     r8, rcx, 48
       mov      rcx, r11
       mov      rdx, r9
       call     [System.SpanHelpers:Memmove(byref,byref,nuint)]
       mov      r8d, dword ptr [rbp+0xFC]
       mov      r10d, dword ptr [rbp+0x268]
       cmp      r8d, r10d
       mov      eax, dword ptr [rbp+0x14C]
       mov      r11d, dword ptr [rbp+0x148]
       jl       G_M000_IG10
       jmp      SHORT G_M000_IG51
 
G_M000_IG49:
       mov      eax, ecx
       jmp      SHORT G_M000_IG48
 
G_M000_IG50:
       mov      esi, 1
       mov      dword ptr [rbp+0xA8], esi
 
G_M000_IG51:
       vmovdqu  ymm0, ymmword ptr [rbp+0x150]
       vmovdqu  ymmword ptr [rbx], ymm0
       vmovdqu  ymm0, ymmword ptr [rbp+0x170]
       vmovdqu  ymmword ptr [rbx+0x20], ymm0
       mov      rax, qword ptr [rbp+0x190]
       mov      qword ptr [rbx+0x40], rax
       vmovsd   qword ptr [rbx+0x08], xmm6
       vmovsd   qword ptr [rbx+0x10], xmm7
       vmovsd   qword ptr [rbx+0x18], xmm8
       vmovsd   qword ptr [rbx+0x20], xmm9
       vmovsd   qword ptr [rbx+0x28], xmm10
       mov      esi, dword ptr [rbp+0xA8]
       mov      byte  ptr [rbx+0x30], sil
       mov      dword ptr [rbx+0x34], -1
       jmp      G_M000_IG37
 
G_M000_IG52:
       mov      ecx, dword ptr [rbp+0xB4]
       jmp      G_M000_IG46
 
G_M000_IG53:
       call     [System.ThrowHelper:ThrowArgumentOutOfRangeException()]
       int3     
 
G_M000_IG54:
       call     [System.ThrowHelper:ThrowArgumentException_DestinationTooShort()]
       int3     
 
G_M000_IG55:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
G_M000_IG56:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
RWD00  	dq	7FF0000000000000h

; Total bytes of code 2333