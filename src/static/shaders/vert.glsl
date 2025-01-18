#version 330 core

layout(location = 0) in vec3 aPos;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;

uniform float animationTime;
uniform float animationDuration;
uniform int keyframeCount;
uniform float keyframeTimes[100];
uniform vec3 translations[100];
uniform vec4 rotations[100];
uniform vec3 scales[100]; // Optional if scale animation is supported
uniform int interpolationMode = 1; // 0 = STEP, 1 = LINEAR, 2 = CUBICSPLINE

// Helper: Spherical Linear Interpolation (SLERP) for quaternions
vec4 slerp(vec4 q1, vec4 q2, float t) {
  float cosTheta = dot(q1, q2);
  if(cosTheta < 0.0) {
    q2 = -q2;
    cosTheta = -cosTheta;
  }
  float angle = acos(cosTheta);
  if(angle < 0.001)
    return normalize(mix(q1, q2, t));
  return normalize((sin((1.0 - t) * angle) * q1 + sin(t * angle) * q2) / sin(angle));
}

// Helper: Cubic Spline Interpolation
vec3 cubicSpline(vec3 prevPoint, vec3 prevTangent, vec3 nextPoint, vec3 nextTangent, float t) {
  float t2 = t * t;
  float t3 = t2 * t;
  return (2.0 * t3 - 3.0 * t2 + 1.0) * prevPoint +
    (t3 - 2.0 * t2 + t) * prevTangent +
    (-2.0 * t3 + 3.0 * t2) * nextPoint +
    (t3 - t2) * nextTangent;
}

// Main function
void main() {
  if(animationDuration <= 0.0 || keyframeCount < 2) {
    gl_Position = projection * view * model * vec4(aPos, 1.0);
    return;
  }

  float normalizedTime = mod(animationTime, animationDuration) / animationDuration;

    // Find the surrounding keyframes
  int startIndex = 0;
  int endIndex = 0;
  for(int i = 0; i < keyframeCount - 1; ++i) {
    if(keyframeTimes[i] <= normalizedTime && keyframeTimes[i + 1] > normalizedTime) {
      startIndex = i;
      endIndex = i + 1;
      break;
    }
  }

  float t = (normalizedTime - keyframeTimes[startIndex]) /
    (keyframeTimes[endIndex] - keyframeTimes[startIndex]);

  vec3 interpolatedTranslation;
  vec4 interpolatedRotation;

  if(interpolationMode == 0) { // STEP interpolation
    interpolatedTranslation = translations[startIndex];
    interpolatedRotation = rotations[startIndex];
  } else if(interpolationMode == 1) { // LINEAR interpolation
    interpolatedTranslation = mix(translations[startIndex], translations[endIndex], t);
    interpolatedRotation = slerp(rotations[startIndex], rotations[endIndex], t);
  } else if(interpolationMode == 2) { // CUBICSPLINE interpolation
    vec3 prevTangent = (keyframeTimes[endIndex] - keyframeTimes[startIndex]) * translations[startIndex];
    vec3 nextTangent = (keyframeTimes[endIndex] - keyframeTimes[startIndex]) * translations[endIndex];
    interpolatedTranslation = cubicSpline(translations[startIndex], prevTangent, translations[endIndex], nextTangent, t);
    interpolatedRotation = slerp(rotations[startIndex], rotations[endIndex], t); // Cubic spline rotation requires additional tangent support
  }

    // Construct transformation matrices
  mat4 translationMatrix = mat4(1.0);
  translationMatrix[3] = vec4(interpolatedTranslation, 1.0);

  mat4 rotationMatrix = mat4(1.0);
  vec4 q = interpolatedRotation;
  rotationMatrix[0] = vec4(1.0 - 2.0 * (q.y * q.y + q.z * q.z), 2.0 * (q.x * q.y - q.w * q.z), 2.0 * (q.x * q.z + q.w * q.y), 0.0);
  rotationMatrix[1] = vec4(2.0 * (q.x * q.y + q.w * q.z), 1.0 - 2.0 * (q.x * q.x + q.z * q.z), 2.0 * (q.y * q.z - q.w * q.x), 0.0);
  rotationMatrix[2] = vec4(2.0 * (q.x * q.z - q.w * q.y), 2.0 * (q.y * q.z + q.w * q.x), 1.0 - 2.0 * (q.x * q.x + q.y * q.y), 0.0);

  mat4 animationTransform = translationMatrix * rotationMatrix;

  gl_Position = projection * view * model * animationTransform * vec4(aPos, 1.0);
}
