; Assembly listing for method OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ComputeBoundedRegion[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+MatchedLinesKernel](OoplesFinance.StockIndicators.Indicators.Bar[],double[][],OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer,int,int,int,System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.IIndicator):OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+PointwiseRegion (Tier1)
; Emitting BLENDED_CODE for generic X64 + VEX on Windows
; Tier1 code
; optimized code
; optimized using Synthesized PGO
; rsp based frame
; partially interruptible
; with Synthesized PGO: fgCalledCount is 15.65229
; 16 inlinees with PGO data; 44 single block inlinees; 0 inlinees without PGO data

G_M000_IG01:
       push     r15
       push     r14
       push     r13
       push     r12
       push     rdi
       push     rsi
       push     rbp
       push     rbx
       sub      rsp, 568
       vmovaps  xmmword ptr [rsp+0x220], xmm6
       vxorps   xmm4, xmm4, xmm4
       vmovdqa  xmmword ptr [rsp+0x70], xmm4
       mov      rax, -336
       vmovdqa  xmmword ptr [rsp+rax+0x1D0], xmm4
       vmovdqa  xmmword ptr [rsp+rax+0x1E0], xmm4
       vmovdqa  xmmword ptr [rsp+rax+0x1F0], xmm4
       add      rax, 48
       jne      SHORT  -5 instr
       mov      qword ptr [rsp+0x1D0], rax
       mov      r14, rcx
       mov      rsi, rdx
       mov      r15, r8
       mov      rbx, r9
       mov      r13d, dword ptr [rsp+0x2A0]
       mov      ebp, dword ptr [rsp+0x2A8]
       mov      rdx, bword ptr [rsp+0x2B8]
       mov      rcx, bword ptr [rsp+0x2C0]
       mov      rdi, gword ptr [rsp+0x2C8]
 
G_M000_IG02:
       mov      r12, bword ptr [rcx]
       mov      eax, dword ptr [rcx+0x08]
       mov      dword ptr [rsp+0xA8], eax
       mov      r8, bword ptr [rdx]
       mov      bword ptr [rsp+0x50], r8
       mov      r10d, dword ptr [rdx+0x08]
       mov      dword ptr [rsp+0xAC], r10d
       vxorps   ymm0, ymm0, ymm0
       vmovdqu  ymmword ptr [rsp+0x1D8], ymm0
       vmovdqu  ymmword ptr [rsp+0x1F8], ymm0
       vmovdqu  xmmword ptr [rsp+0x210], xmm0
       xor      r9d, r9d
       mov      dword ptr [rsp+0x9C], r9d
       mov      dword ptr [rsp+0x98], -1
       vxorps   xmm6, xmm6, xmm6
       mov      edx, r13d
       sub      edx, r10d
       jo       G_M000_IG48
       mov      edx, ebp
       sub      edx, r13d
       jo       G_M000_IG48
       add      edx, r10d
       jo       G_M000_IG48
       mov      rdx, gword ptr [rsp+0x2D0]
       mov      rcx, rdx
       test     rcx, rcx
       je       SHORT G_M000_IG04
 
G_M000_IG03:
       mov      rdx, 0xD1FFAB1E
       cmp      qword ptr [rcx], rdx
       jne      G_M000_IG34
 
G_M000_IG04:
       mov      rdx, rcx
       mov      gword ptr [rsp+0x60], rdx
       mov      rcx, 0xD1FFAB1E
       cmp      qword ptr [rdx], rcx
       jne      G_M000_IG42
       mov      ecx, dword ptr [rdx+0x20]
       mov      dword ptr [rsp+0x124], ecx
       mov      r8d, dword ptr [rdx+0x24]
       mov      dword ptr [rsp+0x120], r8d
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rdx, rax
       mov      gword ptr [rsp+0x58], rdx
       mov      rax, gword ptr [rsp+0x60]
       mov      ecx, dword ptr [rax+0x28]
       mov      eax, dword ptr [rsp+0x124]
       mov      dword ptr [rdx+0x20], eax
       mov      r8d, dword ptr [rsp+0x120]
       mov      dword ptr [rdx+0x24], r8d
       mov      dword ptr [rdx+0x28], ecx
       mov      dword ptr [rdx+0x2C], 1
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rcx, 0xD1FFAB1E
       mov      gword ptr [rax+0x08], rcx
       mov      rdx, gword ptr [rsp+0x58]
       lea      rcx, bword ptr [rdx+0x08]
       mov      rdx, rax
       call     CORINFO_HELP_ASSIGN_REF
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rcx, 0xD1FFAB1E
       mov      gword ptr [rax+0x08], rcx
       mov      rdx, gword ptr [rsp+0x58]
       lea      rcx, bword ptr [rdx+0x10]
       mov      rdx, rax
       call     CORINFO_HELP_ASSIGN_REF
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rcx, 0xD1FFAB1E
       mov      gword ptr [rax+0x08], rcx
       mov      rdx, gword ptr [rsp+0x58]
 
G_M000_IG05:
       lea      rcx, bword ptr [rdx+0x18]
       mov      rdx, rax
       call     CORINFO_HELP_ASSIGN_REF
       mov      rdx, gword ptr [rsp+0x58]
       mov      edx, dword ptr [rdx+0x28]
       lea      rcx, [rsp+0x110]
       call     [System.Numerics.BigInteger:.ctor(int):this]
       mov      rax, gword ptr [rsp+0x110]
       mov      r8d, dword ptr [rsp+0x118]
       test     rax, rax
       jne      G_M000_IG35
       vxorps   xmm0, xmm0, xmm0
       vmovdqu  xmmword ptr [rsp+0xE0], xmm0
       movsxd   rdx, r8d
       lea      rdx, [rdx+4*rdx]
       shl      rdx, 2
       lea      rcx, [rsp+0xE0]
       call     [System.Numerics.BigInteger:.ctor(long):this]
       mov      rdx, gword ptr [rsp+0xE0]
       mov      eax, dword ptr [rsp+0xE8]
       mov      dword ptr [rsp+0x94], eax
 
G_M000_IG06:
       mov      r8, gword ptr [rsp+0x58]
       lea      r10, bword ptr [r8+0x38]
       mov      rcx, r10
       mov      bword ptr [rsp+0x30], rcx
       call     CORINFO_HELP_ASSIGN_REF
       mov      rdx, bword ptr [rsp+0x30]
       mov      ecx, dword ptr [rsp+0x94]
       mov      dword ptr [rdx+0x08], ecx
       mov      rdx, gword ptr [rsp+0x58]
       mov      edx, dword ptr [rdx+0x24]
       lea      rcx, [rsp+0x100]
       call     [System.Numerics.BigInteger:.ctor(int):this]
       mov      rax, gword ptr [rsp+0x100]
       mov      gword ptr [rsp+0x38], rax
       mov      r8d, dword ptr [rsp+0x108]
       mov      dword ptr [rsp+0xA0], r8d
       mov      edx, 5
       mov      ecx, 10
       mov      r10, gword ptr [rsp+0x58]
       cmp      dword ptr [r10+0x2C], 2
       cmovne   edx, ecx
       vxorps   xmm0, xmm0, xmm0
       vmovdqu  xmmword ptr [rsp+0xD0], xmm0
       lea      rcx, [rsp+0xD0]
       call     [System.Numerics.BigInteger:.ctor(int):this]
       mov      rax, gword ptr [rsp+0xD0]
       mov      r8d, dword ptr [rsp+0xD8]
       mov      r10, gword ptr [rsp+0x38]
       test     r10, r10
       jne      G_M000_IG38
       test     rax, rax
       jne      G_M000_IG37
       vxorps   xmm0, xmm0, xmm0
       vmovdqu  xmmword ptr [rsp+0xB0], xmm0
       movsxd   rdx, dword ptr [rsp+0xA0]
       movsxd   rcx, r8d
       imul     rdx, rcx
       lea      rcx, [rsp+0xB0]
       call     [System.Numerics.BigInteger:.ctor(long):this]
       mov      rdx, gword ptr [rsp+0xB0]
       mov      eax, dword ptr [rsp+0xB8]
       mov      dword ptr [rsp+0x90], eax
 
G_M000_IG07:
       mov      r8, gword ptr [rsp+0x58]
       lea      r10, bword ptr [r8+0x48]
       mov      rcx, r10
       mov      bword ptr [rsp+0x28], rcx
       call     CORINFO_HELP_ASSIGN_REF
       mov      rcx, bword ptr [rsp+0x28]
       mov      eax, dword ptr [rsp+0x90]
       mov      dword ptr [rcx+0x08], eax
       mov      rdx, gword ptr [rsp+0x58]
       mov      ecx, dword ptr [rdx+0x20]
       mov      eax, dword ptr [rdx+0x24]
       mov      r8d, dword ptr [rdx+0x28]
       cmp      eax, r8d
       cmovl    eax, r8d
       cmp      ecx, eax
       cmovl    ecx, eax
       mov      eax, 2
       mov      r8d, 1
       cmp      dword ptr [rdx+0x2C], 2
       cmovne   eax, r8d
       add      ecx, eax
       jo       G_M000_IG48
       mov      dword ptr [rdx+0x30], ecx
 
G_M000_IG08:
       mov      rax, rdx
       test     rax, rax
       je       SHORT G_M000_IG10
 
G_M000_IG09:
       mov      rcx, 0xD1FFAB1E
       cmp      qword ptr [rax], rcx
       jne      G_M000_IG43
 
G_M000_IG10:
       mov      gword ptr [rsp+0x48], rax
       cmp      dword ptr [r15+0x08], 0
       jbe      G_M000_IG47
       mov      r15, gword ptr [r15+0x10]
       mov      r8, bword ptr [rsp+0x50]
       mov      bword ptr [rsp+0x40], r8
       mov      r10d, dword ptr [rsp+0xAC]
       mov      dword ptr [rsp+0xA4], r10d
       xor      r9d, r9d
       test     r10d, r10d
       jle      G_M000_IG16
 
G_M000_IG11:
       mov      qword ptr [rsp+0x68], r9
       imul     rdx, r9, 48
       add      rdx, r8
       test     rdi, rdi
       jne      G_M000_IG44
 
G_M000_IG12:
       vmovsd   xmm0, qword ptr [rdx+0x08]
       vmovsd   xmm1, qword ptr [rdx+0x10]
       vmovsd   xmm2, qword ptr [rdx+0x18]
       vbroadcastsd ymm3, qword ptr [reloc @RWD00]
       vunpcklpd xmm0, xmm0, xmm1
       vmovsd   xmm1, qword ptr [rdx+0x20]
       vunpcklpd xmm1, xmm2, xmm1
       vinsertf128 ymm0, ymm0, xmm1
       vpand    ymm0, ymm0, ymm3
       vpcmpeqq ymm0, ymm0, ymm3
       vptest   ymm0, ymm0
       jne      SHORT G_M000_IG15
       mov      rcx, qword ptr [rdx+0x28]
       mov      r11, 0xD1FFAB1E
       andn     rcx, rcx, r11
       je       SHORT G_M000_IG15
 
G_M000_IG13:
       mov      rcx, rax
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.MatchedLinesState:Update(byref):double:this]
       mov      rdx, qword ptr [rsp+0x68]
       inc      edx
       mov      ecx, dword ptr [rsp+0xA4]
       cmp      edx, ecx
       mov      r9, rdx
       mov      r8, bword ptr [rsp+0x40]
       jge      G_M000_IG16
 
G_M000_IG14:
       mov      rax, gword ptr [rsp+0x48]
       jmp      G_M000_IG11
 
G_M000_IG15:
       vxorps   ymm0, ymm0, ymm0
       vmovdqu  ymmword ptr [rsp+0x158], ymm0
       vmovdqu  ymmword ptr [rsp+0x178], ymm0
       vmovdqu  xmmword ptr [rsp+0x190], xmm0
       vmovdqu  ymm0, ymmword ptr [rdx]
       vmovdqu  ymmword ptr [rsp+0x158], ymm0
       vmovdqu  xmm0, xmmword ptr [rdx+0x20]
       vmovdqu  xmmword ptr [rsp+0x178], xmm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x158]
       vmovdqu  ymmword ptr [r14], ymm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x178]
       vmovdqu  ymmword ptr [r14+0x20], ymm0
       mov      rdx, qword ptr [rsp+0x198]
       mov      qword ptr [r14+0x40], rdx
       mov      byte  ptr [r14+0x30], 1
       mov      dword ptr [r14+0x34], -1
       xor      edx, edx
       mov      qword ptr [r14+0x38], rdx
       jmp      G_M000_IG32
 
G_M000_IG16:
       vxorps   ymm0, ymm0, ymm0
       vmovdqu  ymmword ptr [rsp+0x1A8], ymm0
       vmovdqu  xmmword ptr [rsp+0x1C8], xmm0
       mov      eax, dword ptr [rsp+0xA8]
       mov      r8d, ebp
       sub      r8d, eax
       jo       G_M000_IG48
       mov      dword ptr [rsp+0x1A4], r8d
       mov      r13d, r13d
       cmp      r13d, ebp
       jl       G_M000_IG21
       jmp      G_M000_IG30
 
G_M000_IG17:
       cmp      gword ptr [rbx+0x10], 0
       jne      G_M000_IG46
       mov      rax, gword ptr [rbx+0x08]
       mov      edx, r13d
       sar      edx, 10
       cmp      edx, dword ptr [rax+0x08]
       jae      G_M000_IG47
       mov      rax, gword ptr [rax+8*rdx+0x10]
       mov      edx, r13d
       and      edx, 0x3FF
       cmp      edx, dword ptr [rax+0x08]
       jae      G_M000_IG47
       imul     rdx, rdx, 48
       lea      rax, bword ptr [rax+rdx+0x10]
 
G_M000_IG18:
       vmovdqu  ymm1, ymmword ptr [rsp+0x1A8]
       vmovdqu  ymmword ptr [rax], ymm1
       vmovdqu  xmm1, xmmword ptr [rsp+0x1C8]
       vmovdqu  xmmword ptr [rax+0x20], xmm1
 
G_M000_IG19:
       vmovq    rax, xmm0
       mov      rdx, 0xD1FFAB1E
       andn     rax, rax, rdx
       je       G_M000_IG27
 
G_M000_IG20:
       add      r13d, 1
       jo       G_M000_IG48
       cmp      r13d, ebp
       mov      eax, dword ptr [rsp+0xA8]
       mov      r8d, dword ptr [rsp+0x1A4]
       jge      G_M000_IG30
 
G_M000_IG21:
       test     rdi, rdi
       jne      G_M000_IG45
 
G_M000_IG22:
       cmp      r13d, r8d
       jge      G_M000_IG26
 
G_M000_IG23:
       cmp      r13d, dword ptr [rsi+0x08]
       jae      G_M000_IG47
       imul     rdx, r13, 48
       vmovdqu  ymm0, ymmword ptr [rsi+rdx+0x10]
       vmovdqu  ymmword ptr [rsp+0x128], ymm0
       vmovdqu  xmm0, xmmword ptr [rsi+rdx+0x30]
       vmovdqu  xmmword ptr [rsp+0x148], xmm0
 
G_M000_IG24:
       vmovdqu  ymm0, ymmword ptr [rsp+0x128]
       vmovdqu  ymmword ptr [rsp+0x1A8], ymm0
       vmovdqu  xmm0, xmmword ptr [rsp+0x148]
       vmovdqu  xmmword ptr [rsp+0x1C8], xmm0
       vmovsd   xmm0, qword ptr [rsp+0x1B0]
       vmovsd   xmm1, qword ptr [rsp+0x1B8]
       vmovsd   xmm2, qword ptr [rsp+0x1C0]
       vbroadcastsd ymm3, qword ptr [reloc @RWD00]
       vunpcklpd xmm0, xmm0, xmm1
       vmovsd   xmm1, qword ptr [rsp+0x1C8]
       vunpcklpd xmm1, xmm2, xmm1
       vinsertf128 ymm0, ymm0, xmm1
       vpand    ymm0, ymm0, ymm3
       vpcmpeqq ymm0, ymm0, ymm3
       vptest   ymm0, ymm0
       jne      G_M000_IG29
       mov      rdx, qword ptr [rsp+0x1D0]
       mov      rcx, 0xD1FFAB1E
       andn     rdx, rdx, rcx
       je       G_M000_IG29
 
G_M000_IG25:
       lea      rdx, [rsp+0x1A8]
       mov      rcx, gword ptr [rsp+0x48]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Indicators.MatchedLinesState:Update(byref):double:this]
       cmp      r13d, dword ptr [r15+0x08]
       jae      G_M000_IG47
       vmovsd   qword ptr [r15+8*r13+0x10], xmm0
       test     rbx, rbx
       je       G_M000_IG19
       jmp      G_M000_IG17
 
G_M000_IG26:
       mov      edx, r13d
       sub      edx, r8d
       jo       G_M000_IG48
       cmp      edx, eax
       jae      G_M000_IG47
       imul     rdx, rdx, 48
       vmovdqu  ymm0, ymmword ptr [r12+rdx]
       vmovdqu  ymmword ptr [rsp+0x128], ymm0
       vmovdqu  xmm0, xmmword ptr [r12+rdx+0x20]
       vmovdqu  xmmword ptr [rsp+0x148], xmm0
       mov      eax, dword ptr [rsp+0xA8]
       mov      r8d, dword ptr [rsp+0x1A4]
       jmp      G_M000_IG24
 
G_M000_IG27:
       mov      eax, dword ptr [rsp+0x98]
       test     eax, eax
       mov      dword ptr [rsp+0x98], eax
       jge      G_M000_IG20
 
G_M000_IG28:
       mov      eax, r13d
       vmovaps  xmm6, xmm0
       mov      dword ptr [rsp+0x98], eax
       jmp      G_M000_IG20
 
G_M000_IG29:
       mov      ebx, 1
       mov      dword ptr [rsp+0x9C], ebx
 
G_M000_IG30:
       vmovdqu  ymm0, ymmword ptr [rsp+0x1A8]
       vmovdqu  ymmword ptr [rsp+0x1D8], ymm0
       vmovdqu  xmm0, xmmword ptr [rsp+0x1C8]
       vmovdqu  xmmword ptr [rsp+0x1F8], xmm0
 
G_M000_IG31:
       vmovdqu  ymm0, ymmword ptr [rsp+0x1D8]
       vmovdqu  ymmword ptr [r14], ymm0
       vmovdqu  ymm0, ymmword ptr [rsp+0x1F8]
       vmovdqu  ymmword ptr [r14+0x20], ymm0
       mov      rdx, qword ptr [rsp+0x218]
       mov      qword ptr [r14+0x40], rdx
       mov      ebx, dword ptr [rsp+0x9C]
       mov      byte  ptr [r14+0x30], bl
       mov      eax, dword ptr [rsp+0x98]
       mov      dword ptr [r14+0x34], eax
       vmovsd   qword ptr [r14+0x38], xmm6
 
G_M000_IG32:
       mov      rax, r14
 
G_M000_IG33:
       vzeroupper 
       vmovaps  xmm6, xmmword ptr [rsp+0x220]
       add      rsp, 568
       pop      rbx
       pop      rbp
       pop      rsi
       pop      rdi
       pop      r12
       pop      r13
       pop      r14
       pop      r15
       ret      
 
G_M000_IG34:
       mov      rdx, gword ptr [rsp+0x2D0]
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_CHKCASTCLASS
       mov      rcx, rax
       jmp      G_M000_IG04
 
G_M000_IG35:
       lea      rdx, bword ptr [rax+0x10]
       mov      r9d, dword ptr [rax+0x08]
       mov      bword ptr [rsp+0x80], rdx
       mov      dword ptr [rsp+0x88], r9d
       xor      rdx, rdx
       mov      bword ptr [rsp+0x70], rdx
 
G_M000_IG36:
       mov      dword ptr [rsp+0x78], edx
       mov      dword ptr [rsp+0x20], 20
       lea      rdx, [rsp+0x80]
       lea      r9, [rsp+0x70]
       lea      rcx, [rsp+0xF0]
       call     [System.Numerics.BigInteger:Multiply(System.ReadOnlySpan`1[uint],int,System.ReadOnlySpan`1[uint],int):System.Numerics.BigInteger]
       mov      rdx, gword ptr [rsp+0xF0]
       mov      eax, dword ptr [rsp+0xF8]
       mov      dword ptr [rsp+0x94], eax
       jmp      G_M000_IG06
 
G_M000_IG37:
       xor      rdx, rdx
       xor      r9d, r9d
       jmp      SHORT G_M000_IG39
 
G_M000_IG38:
       lea      rdx, bword ptr [r10+0x10]
       mov      r9d, dword ptr [r10+0x08]
 
G_M000_IG39:
       test     rax, rax
       jne      SHORT G_M000_IG40
       xor      rcx, rcx
       xor      eax, eax
       jmp      SHORT G_M000_IG41
 
G_M000_IG40:
       lea      rcx, bword ptr [rax+0x10]
       mov      eax, dword ptr [rax+0x08]
 
G_M000_IG41:
       mov      bword ptr [rsp+0x80], rdx
       mov      dword ptr [rsp+0x88], r9d
       mov      bword ptr [rsp+0x70], rcx
       mov      dword ptr [rsp+0x78], eax
       mov      dword ptr [rsp+0x20], r8d
       lea      rdx, [rsp+0x80]
       lea      r9, [rsp+0x70]
       lea      rcx, [rsp+0xC0]
       mov      r8d, dword ptr [rsp+0xA0]
       call     [System.Numerics.BigInteger:Multiply(System.ReadOnlySpan`1[uint],int,System.ReadOnlySpan`1[uint],int):System.Numerics.BigInteger]
       mov      rdx, gword ptr [rsp+0xC0]
       mov      eax, dword ptr [rsp+0xC8]
       mov      dword ptr [rsp+0x90], eax
       jmp      G_M000_IG07
 
G_M000_IG42:
       mov      rcx, rdx
       mov      rdx, qword ptr [rdx]
       mov      rdx, qword ptr [rdx+0x48]
       call     [rdx]OoplesFinance.StockIndicators.Indicators.IndicatorBase:CreateState():System.Object:this
       mov      rdx, rax
       jmp      G_M000_IG08
 
G_M000_IG43:
       call     CORINFO_HELP_CHKCASTCLASS
       int3     
 
G_M000_IG44:
       cmp      dword ptr [rdi+0x20], 0
       je       G_M000_IG12
       jmp      G_M000_IG31
 
G_M000_IG45:
       cmp      dword ptr [rdi+0x20], 0
       jne      G_M000_IG30
       jmp      G_M000_IG22
 
G_M000_IG46:
       mov      rax, gword ptr [rbx+0x10]
       cmp      r13d, dword ptr [rax+0x08]
       jae      SHORT G_M000_IG47
       imul     rdx, r13, 48
       lea      rax, bword ptr [rax+rdx+0x10]
       jmp      G_M000_IG18
 
G_M000_IG47:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
G_M000_IG48:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
RWD00  	dq	7FF0000000000000h

; Total bytes of code 2435

