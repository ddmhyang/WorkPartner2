// TintColor.fx

// C# 코드에서 채워줄 변수 (Parameter)
float4 TintColor : register(C0);

// WPF에서 텍스처를 전달받기 위한 샘플러
sampler2D implicitInput : register(S0);

// 픽셀 셰이더 메인 함수
float4 main(float2 uv : TEXCOORD) : COLOR
{
    // 1. 원본 텍스처(회색조 머리 이미지)에서 현재 픽셀의 색상을 가져옵니다.
    float4 originalColor = tex2D(implicitInput, uv);
    
    // 2. 원본 색상에 TintColor를 곱합니다.
    //    회색조 이미지는 R,G,B 값이 모두 같으므로, TintColor를 곱하면 해당 색으로 자연스럽게 물듭니다.
    float4 outputColor = originalColor * TintColor;
    
    // 3. 원본의 투명도(Alpha)는 그대로 유지합니다.
    outputColor.a = originalColor.a;

    return outputColor;
}
